#include "infer.h"
#include "cplm.h"
#ifdef __AVX2__
#    include <immintrin.h>
#endif

namespace cplm
{
namespace
{
    typedef float (*dotprod_t)(void* w, int32_t n, int32_t i, float* x);

    inline float to_float(int16_t x)
    {
        __m128i r = _mm_set1_epi16(x);
        float f[4];
        _mm_storeu_ps(f, _mm_cvtph_ps(r));
        return f[0];
    }

    inline int16_t to_half(float x)
    {
        __m128 r = _mm_set1_ps(x);
        int16_t s[8];
        _mm_storeu_epi16(s, _mm_cvtps_ph(r, _MM_FROUND_TO_NEAREST_INT));
        return s[0];
    }

    inline float fp82float(int16_t v)
    {
        int16_t u = v;
        u <<= 8;
        __m128i r = _mm_set1_epi16(u);
        float f[4];
        _mm_storeu_ps(f, _mm_cvtph_ps(r));
        return f[0];
    }

    inline float gf4_ff(uint32_t v, int32_t k)
    {
        float s = fp82float(v & 0xff) / -4.f; // we expect compiler to reuse this across multiple calls
        return ((int32_t)((v >> (8 + k * 3)) & 7) - 4) * s;
    }

    float dotprod_fp16(void* w, int32_t n, int32_t i, float* x)
    {
        int16_t* r = (int16_t*)w + i * n;
#if defined(__AVX__)
        assert(n % 16 == 0);
        __m256 acc0 = _mm256_setzero_ps(), acc1 = _mm256_setzero_ps();
        for(int32_t j = 0; j < n; j += 16) {
            __m256i rw = _mm256_loadu_si256((__m256i*)&r[j]);
            __m128i rlo = _mm256_castsi256_si128(rw);
            __m128i rhi = _mm256_extractf128_si256(rw, 1);
            __m256 x0 = _mm256_loadu_ps(&x[j]);
            __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
            #if 0
            acc0 = _mm256_add_ps(_mm256_mul_ps(x0, _mm256_cvtph_ps(rlo)), acc0);
            acc1 = _mm256_add_ps(_mm256_mul_ps(x1, _mm256_cvtph_ps(rhi)), acc1);
            #else
            acc0 = _mm256_fmadd_ps(x0, _mm256_cvtph_ps(rlo), acc0);
            acc1 = _mm256_fmadd_ps(x1, _mm256_cvtph_ps(rhi), acc1);
            #endif
        }
        __m256 acc8 = _mm256_add_ps(acc0, acc1);
        __m128 acc4 = _mm_add_ps(_mm256_castps256_ps128(acc8), _mm256_extractf128_ps(acc8, 1));
        __m128 accf = _mm_dp_ps(acc4, _mm_set1_ps(1.0f), 0xf1);
        return _mm_cvtss_f32(accf);
#else
        float val = 0.0f;
        #pragma omp simd reduction(+ : val) simdlen(32)
        for(int32_t j = 0; j < n; ++j) {
            val += r[j] * x[j];
        }
        return val;
#endif
    }

    float dotprod_fp8(void* w, int32_t n, int32_t i, float* x)
    {
        char* r = (char*)w + i * n;
#if defined(__AVX2__)
        assert(n % 16 == 0);
        __m256 acc0 = _mm256_setzero_ps();
        __m256 acc1 = _mm256_setzero_ps();
        __m128i izero = _mm_setzero_si128();
        for(int32_t j = 0; j < n; j += 16) {
            __m128i rw = _mm_loadu_si128((__m128i*)&r[j]);
            __m128i rlo = _mm_unpacklo_epi8(izero, rw);
            __m128i rhi = _mm_unpackhi_epi8(izero, rw);
            __m256 x0 = _mm256_loadu_ps(&x[j]);
            __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
            #if 0
            acc0 = _mm256_add_ps(_mm256_mul_ps(x0, _mm256_cvtph_ps(rlo)), acc0);
            acc1 = _mm256_add_ps(_mm256_mul_ps(x1, _mm256_cvtph_ps(rhi)), acc1);
            #else
            acc0 = _mm256_fmadd_ps(x0, _mm256_cvtph_ps(rlo), acc0);
            acc1 = _mm256_fmadd_ps(x1, _mm256_cvtph_ps(rhi), acc1);
            #endif
        }
        __m256 acc8 = _mm256_add_ps(acc0, acc1);
        __m128 acc4 = _mm_add_ps(_mm256_castps256_ps128(acc8), _mm256_extractf128_ps(acc8, 1));
        __m128 accf = _mm_dp_ps(acc4, _mm_set1_ps(1.0f), 0xf1);
        return _mm_cvtss_f32(accf);
#else
        float val = 0.0f;
        #pragma omp simd reduction(+ : val) simdlen(32)
        for(int32_t j = 0; j < n; j++) {
            val += fp82half(r[j]) * x[j];
        }
        return val;
#endif
    }

    float dotprod_gf4(void* w, int32_t n, int32_t i, float* x)
    {
        uint32_t* r = (uint32_t*)w + i * n / 8;
#if defined(__AVX2__)
        assert(n % 32 == 0);
        __m256 acc0 = _mm256_setzero_ps(), acc1 = _mm256_setzero_ps();
        for(int32_t j = 0; j < n; j += 32) {
            __m128i wg = _mm_loadu_si128((__m128i*)&r[j / 8]);
            const __m128i wgfm = _mm_setr_epi8(-1, 0, -1, 4, -1, 8, -1, 12, -1, -1, -1, -1, -1, -1, -1, -1);
            __m128 wgf = _mm_cvtph_ps(_mm_shuffle_epi8(wg, wgfm)); // note: scale 1/-4.f is baked into wgtab below
            __m256 x0 = _mm256_loadu_ps(&x[j]);
            __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
            __m256 x2 = _mm256_loadu_ps(&x[j + 16]);
            __m256 x3 = _mm256_loadu_ps(&x[j + 24]);
            __m256i wgp = _mm256_broadcastsi128_si256(wg);
            __m256 wgfp = _mm256_castsi256_ps(_mm256_broadcastsi128_si256(_mm_castps_si128(wgf)));
            const __m256i wgbits = _mm256_setr_epi32(8, 11, 14, 17, 20, 23, 26, 29);
            const __m256 wgtab = _mm256_setr_ps(-4 / -4.f, -3 / -4.f, -2 / -4.f, -1 / -4.f, 0 / -4.f, 1 / -4.f, 2 / -4.f, 3 / -4.f);
            __m256 w0 = _mm256_permutevar8x32_ps(wgtab, _mm256_srlv_epi32(_mm256_shuffle_epi32(wgp, 0x00), wgbits));
            __m256 w1 = _mm256_permutevar8x32_ps(wgtab, _mm256_srlv_epi32(_mm256_shuffle_epi32(wgp, 0x55), wgbits));
            __m256 w2 = _mm256_permutevar8x32_ps(wgtab, _mm256_srlv_epi32(_mm256_shuffle_epi32(wgp, 0xaa), wgbits));
            __m256 w3 = _mm256_permutevar8x32_ps(wgtab, _mm256_srlv_epi32(_mm256_shuffle_epi32(wgp, 0xff), wgbits));
            #if 0
            acc0 = _mm256_add_ps(_mm256_mul_ps(w0, _mm256_mul_ps(x0, _mm256_shuffle_ps(wgfp, wgfp, 0x00))), acc0);
            acc1 = _mm256_add_ps(_mm256_mul_ps(w1, _mm256_mul_ps(x1, _mm256_shuffle_ps(wgfp, wgfp, 0x55))), acc1);
            acc0 = _mm256_add_ps(_mm256_mul_ps(w2, _mm256_mul_ps(x2, _mm256_shuffle_ps(wgfp, wgfp, 0xaa))), acc0);
            acc1 = _mm256_add_ps(_mm256_mul_ps(w3, _mm256_mul_ps(x3, _mm256_shuffle_ps(wgfp, wgfp, 0xff))), acc1);
            #else
            acc0 = _mm256_fmadd_ps(w0, _mm256_mul_ps(x0, _mm256_shuffle_ps(wgfp, wgfp, 0x00)), acc0);
            acc1 = _mm256_fmadd_ps(w1, _mm256_mul_ps(x1, _mm256_shuffle_ps(wgfp, wgfp, 0x55)), acc1);
            acc0 = _mm256_fmadd_ps(w2, _mm256_mul_ps(x2, _mm256_shuffle_ps(wgfp, wgfp, 0xaa)), acc0);
            acc1 = _mm256_fmadd_ps(w3, _mm256_mul_ps(x3, _mm256_shuffle_ps(wgfp, wgfp, 0xff)), acc1);
            #endif
        }
        __m256 acc8 = _mm256_add_ps(acc0, acc1);
        __m128 acc4 = _mm_add_ps(_mm256_castps256_ps128(acc8), _mm256_extractf128_ps(acc8, 1));
        __m128 accf = _mm_dp_ps(acc4, _mm_set1_ps(1.0f), 0xf1);
        return _mm_cvtss_f32(accf);
#else
        float val = 0.0f;
        for(int32_t j = 0; j < n; j += 8) {
            uint32_t wg = r[j / 8];
            for(int32_t k = 0; k < 8; ++k) {
                val += gf4_ff(wg, k) * x[j + k];
            }
        }
        return val;
#endif
    }

    void rmsnorm(float* o, float* x, float* weight, int32_t size, float eps, bool ln)
    {
#if defined(__AVX2__)
        assert(size % 16 == 0);
        // calculate mean
        float mean = 0.0f;
        if(ln) {
            __m256 acc0 = _mm256_setzero_ps();
            __m256 acc1 = _mm256_setzero_ps();
            for(int32_t j = 0; j < size; j += 16) {
                __m256 x0 = _mm256_loadu_ps(&x[j]);
                __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
                acc0 = _mm256_add_ps(x0, acc0);
                acc1 = _mm256_add_ps(x1, acc1);
            }
            __m256 acc8 = _mm256_add_ps(acc0, acc1);
            __m128 acc4 = _mm_add_ps(_mm256_castps256_ps128(acc8), _mm256_extractf128_ps(acc8, 1));
            float acc[4];
            _mm_storeu_ps(acc, acc4);
            mean = acc[0] + acc[1] + acc[2] + acc[3];
            mean /= size;
        }
        // calculate sum of squared deltas
        float var = 0.0f;
        __m256 m8 = _mm256_set1_ps(mean);
        {
            __m256 acc0 = _mm256_setzero_ps();
            __m256 acc1 = _mm256_setzero_ps();
            for(int32_t j = 0; j < size; j+=16) {
                __m256 x0 = _mm256_loadu_ps(&x[j]);
                __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
                __m256 d0 = _mm256_sub_ps(x0, m8);
                __m256 d1 = _mm256_sub_ps(x1, m8);
                acc0 = _mm256_fmadd_ps(d0, d0, acc0);
                acc1 = _mm256_fmadd_ps(d1, d1, acc1);
            }
            __m256 acc8 = _mm256_add_ps(acc0, acc1);
            __m128 acc4 = _mm_add_ps(_mm256_castps256_ps128(acc8), _mm256_extractf128_ps(acc8, 1));
            float acc[4];
            _mm_storeu_ps(acc, acc4);
            float ss = acc[0] + acc[1] + acc[2] + acc[3];
            var = ss / size;
        }

        // normalize and scale
        {
            float scale = 1.0f / sqrtf(var + eps);
            __m256 s8 = _mm256_set1_ps(scale);
            for(int32_t j = 0; j < size; j += 16) {
                __m256 x0 = _mm256_loadu_ps(&x[j]);
                __m256 x1 = _mm256_loadu_ps(&x[j + 8]);
                __m256 w0 = _mm256_loadu_ps(&weight[j]);
                __m256 w1 = _mm256_loadu_ps(&weight[j + 8]);
                __m256 d0 = _mm256_sub_ps(x0, m8);
                __m256 d1 = _mm256_sub_ps(x1, m8);
                d0 = _mm256_mul_ps(d0, s8);
                d1 = _mm256_mul_ps(d1, s8);

                _mm256_storeu_ps(&o[j], _mm256_mul_ps(d0, w0));
                _mm256_storeu_ps(&o[j + 8], _mm256_mul_ps(d1, w1));
            }
        }
#else
        // calculate mean
        float mean = 0.0f;
        if(ln) {
            for(int32_t j = 0; j < size; j++) {
                mean += x[j];
            }
            mean /= size;
        }

        // calculate sum of squared deltas
        float ss = 0.0f;
        for(int32_t j = 0; j < size; j++) {
            ss += (x[j] - mean) * (x[j] - mean);
        }

        float var = ss / size;

        // normalize and scale
        float scale = 1.0f / sqrtf(var + eps);
        for(int32_t j = 0; j < size; j++) {
            o[j] = (x[j] - mean) * scale * weight[j];
        }
#endif
    }

    void matmul(float* xout, float* x, void* w, float* b, int32_t n, int32_t d, dotprod_t dotprod)
    {
        // W (d,n) @ x (n,) -> xout (d,)
        // by far the most amount of time is spent inside this little function
        int32_t i;
        #pragma omp parallel for private(i)
        for(i = 0; i < d; ++i) {
            float val = dotprod(w, n, i, x);
            if(b) {
                val += b[i];
            }
            xout[i] = val;
        }
    }

    void rope(float* vec, int32_t d, int32_t head_dim, int32_t pos, float theta, int32_t rotary_dim)
    {
#if defined(__AVX2__)
        assert(d % 8 == 0);
        __m128 pos4 = _mm_cvtepi32_ps(_mm_set1_epi32(pos));
        for(int32_t i = 0; i < d; i += 8) {

            int32_t j_head0 = (i+0) % head_dim;
            int32_t j_head1 = (i+2) % head_dim;
            int32_t j_head2 = (i+4) % head_dim;
            int32_t j_head3 = (i+6) % head_dim;

            float freq[4];
            freq[0] = j_head0 >= rotary_dim ? 0.f : 1.0f / powf(theta, (float)j_head0 / (float)rotary_dim);
            freq[1] = j_head1 >= rotary_dim ? 0.f : 1.0f / powf(theta, (float)j_head1 / (float)rotary_dim);
            freq[2] = j_head2 >= rotary_dim ? 0.f : 1.0f / powf(theta, (float)j_head2 / (float)rotary_dim);
            freq[3] = j_head3 >= rotary_dim ? 0.f : 1.0f / powf(theta, (float)j_head3 / (float)rotary_dim);
            __m128 freq4 = _mm_loadu_ps(freq);
            __m128 val = _mm_mul_ps(pos4, freq4);
            __m128 fcr4;
            __m128 fci4 = _mm_sincos_ps(&fcr4, val);
            float fcr[4];
            float fci[4];
            _mm_storeu_ps(fcr, fcr4);
            _mm_storeu_ps(fci, fci4);

            {
                float v0 = vec[i + 0];
                float v1 = vec[i + 1];
                vec[i + 0] = v0 * fcr[0] - v1 * fci[0];
                vec[i + 1] = v0 * fci[0] + v1 * fcr[0];
            }
            {
                float v0 = vec[i + 2];
                float v1 = vec[i + 3];
                vec[i + 2] = v0 * fcr[1] - v1 * fci[1];
                vec[i + 3] = v0 * fci[1] + v1 * fcr[1];
            }
            {
                float v0 = vec[i + 4];
                float v1 = vec[i + 5];
                vec[i + 4] = v0 * fcr[2] - v1 * fci[2];
                vec[i + 5] = v0 * fci[2] + v1 * fcr[2];
            }
            {
                float v0 = vec[i + 6];
                float v1 = vec[i + 7];
                vec[i + 6] = v0 * fcr[3] - v1 * fci[3];
                vec[i + 7] = v0 * fci[3] + v1 * fcr[3];
            }
        }
#else
        for(int32_t i = 0; i < d; i += 2) {
            int32_t j_head = i % head_dim;
            float freq = j_head >= rotary_dim ? 0.f : 1.0f / powf(theta, (float)j_head / (float)rotary_dim);
            float val = pos * freq;
            float fcr = cosf(val);
            float fci = sinf(val);

            float v0 = vec[i];
            float v1 = vec[i + 1];
            vec[i] = v0 * fcr - v1 * fci;
            vec[i + 1] = v0 * fci + v1 * fcr;
        }
#endif
    }

    void attn(float* xout, float* atth, float* qh, int16_t* kh, int16_t* vh, int32_t head_dim, int32_t kv_dim, int32_t kv_len)
    {
        float score_max = std::numeric_limits<float>::lowest();

        // calculate attention scores as dot products of q and k; also track score max for this head
        for(int32_t t = 0; t < kv_len; ++t) {
            float score = 0.0f;
            for(int32_t j = 0; j < head_dim; ++j) {
                float f = to_float(kh[t * kv_dim + j]);
                assert(!isnan(f));
                score += qh[j] * f;
            }
            score /= sqrtf(static_cast<float>(head_dim));
            score_max = (score_max < score) ? score : score_max;
            atth[t] = score;
        }

        // softmax the scores to get attention weights over [0..kv_len)
        float score_sum = 0.f;
        for(int32_t t = 0; t < kv_len; ++t) {
            atth[t] = expf(atth[t] - score_max);
            score_sum += atth[t];
        }

        // mix values with attention weights
        for(int32_t j = 0; j < head_dim; ++j) {
            float res = 0.f;
            for(int32_t t = 0; t < kv_len; ++t) {
                res += (atth[t] / score_sum) * to_float(vh[t * kv_dim + j]);
            }
            xout[j] = res;
        }
    }

    inline float gelu(float x)
    {
        return 0.5f * x * (1.0f + tanhf(0.797885f * (x + 0.044715f * x * x * x)));
    }

    inline float silu(float x)
    {
        return x / (1.0f + expf(-x));
    }

    void moe_gate(float* moe_weights, int32_t* moe_experts, float* x, int32_t d, int32_t active)
    {
        // softmax across experts
        float max_val = -FLT_MAX;
        for(int32_t j = 0; j < d; ++j) {
            max_val = (max_val < x[j]) ? x[j] : max_val;
        }

        // top k
        uint64_t mask = 0;
        float wsum = 0.0f;

        for(int32_t k = 0; k < active; ++k) {
            int32_t best = -1;
            for(int32_t j = 0; j < d; ++j) {
                if((mask & (1ull << j)) == 0 && (best == -1 || x[j] > x[best])) {
                    best = j;
                }
            }

            moe_experts[k] = best;
            wsum += expf(x[moe_experts[k]] - max_val);
            mask |= 1ull << best;
        }

        // top k weights, normalized
        for(int32_t k = 0; k < active; ++k) {
            moe_weights[k] = expf(x[moe_experts[k]] - max_val) / wsum;
        }
    }

    inline float clip(float x, float v)
    {
        return x < -v ? -v : (x > v ? v : x);
    }

} // namespace

float* forward(::Transformer* transformer, int32_t token, int32_t pos, uint32_t flags)
{
    if(transformer->weights_.dbits_ != 4 && transformer->weights_.dbits_ != 8 && transformer->weights_.dbits_ != 16) {
        assert(!"Unsupported dbits: must be 8 or 16 for CPU");
    }

    dotprod_t dotprod = transformer->weights_.dbits_ == 4 ? dotprod_gf4 : (transformer->weights_.dbits_ == 8 ? dotprod_fp8 : dotprod_fp16);

    // a few convenience variables
    Config* p = &transformer->config_;
    Weights* w = &transformer->weights_;
    RunState* s = &transformer->state_;
    float* x = s->x_;
    int32_t dim = p->dim_;
    int32_t hidden_dim = p->hidden_dim_;
    int32_t q_dim = p->head_dim_ * p->n_heads_;
    int32_t kv_dim = p->head_dim_ * p->n_kv_heads_;
    int32_t kv_mul = p->n_heads_ / p->n_kv_heads_; // integer multiplier of the kv sharing in multiquery

    // following "attention sinks" from StreamingLLM we keep the first few tokens in the KV cache as is
    int32_t kv_sink = pos >= p->seq_len_ ? CPLM_KV_SINKS : 0;
    int32_t kv_pos = kv_sink + (pos - kv_sink) % (p->seq_len_ - kv_sink);
    int32_t kv_len = pos >= p->seq_len_ ? p->seq_len_ : pos + 1;

    // copy the token embedding into x
    const uint8_t* content_row = (const uint8_t*)w->token_embedding_table_ + token * dim * (size_t)w->dbits_ / 8;
    if(w->dbits_ == 4) {
        for(int32_t i = 0; i < dim; i += 8) {
            uint32_t wg = ((uint32_t*)content_row)[i / 8];
            for(int32_t k = 0; k < 8; ++k) {
                x[i + k] = gf4_ff(wg, k);
            }
        }
    } else {
        for(int32_t i = 0; i < dim; ++i) {
            x[i] = w->dbits_ == 8 ? fp82float(content_row[i]) : to_float(((int16_t*)content_row)[i]);
        }
    }

    // forward all the layers
    for(int32_t l = 0; l < p->n_layers_; ++l) {
        // attention rmsnorm
        rmsnorm(s->xb_, x, w->rms_att_weight_[l], dim, p->norm_eps_, p->norm_ln_);

        // key and value point to the kv cache
        size_t loff = (size_t)l * p->seq_len_ * kv_dim; // kv cache layer offset for convenience
        int16_t* kb = (int16_t*)s->key_cache_ + loff;
        int16_t* vb = (int16_t*)s->value_cache_ + loff;

        // qkv matmuls for this position
        matmul(s->q_, s->xb_, w->wq_[l], w->bqkv_[l], dim, q_dim, dotprod);
        matmul(s->k_, s->xb_, w->wk_[l], w->bqkv_[l] ? w->bqkv_[l] + q_dim : nullptr, dim, kv_dim, dotprod);
        matmul(s->v_, s->xb_, w->wv_[l], w->bqkv_[l] ? w->bqkv_[l] + q_dim + kv_dim : nullptr, dim, kv_dim, dotprod);

        // some models require clipping qkv values
        for(int32_t i = 0; i < q_dim; ++i) {
            s->q_[i] = clip(s->q_[i], p->qkv_clip_);
        }
        for(int32_t i = 0; i < kv_dim; ++i) {
            s->k_[i] = clip(s->k_[i], p->qkv_clip_);
            s->v_[i] = clip(s->v_[i], p->qkv_clip_);
        }

        // RoPE relative positional encoding: complex-valued rotate q and k in each head
        rope(s->q_, q_dim, p->head_dim_, pos, p->rope_theta_, p->rotary_dim_);
        rope(s->k_, kv_dim, p->head_dim_, pos, p->rope_theta_, p->rotary_dim_);

        // update kv cache
        for(int32_t i = 0; i < kv_dim; i++) {
            kb[kv_pos * kv_dim + i] = to_half(s->k_[i]);
            vb[kv_pos * kv_dim + i] = to_half(s->v_[i]);
        }

        // rotate sink tokens forward to keep pace with non-sink tokens
        for(int32_t r = 0; r < kv_sink; r++) {
            for(int32_t i = 0; i < kv_dim; i++) {
                s->k_[i] = to_float(kb[r * kv_dim + i]);
            }

            rope(s->k_, kv_dim, p->head_dim_, 1, p->rope_theta_, p->rotary_dim_);

            for(int32_t i = 0; i < kv_dim; i++) {
                kb[r * kv_dim + i] = to_half(s->k_[i]);
            }
        }

        // multihead attention. iterate over all heads
        int32_t h;
        #pragma omp parallel for private(h)
        for(h = 0; h < p->n_heads_; h++) {
            float* qh = s->q_ + h * p->head_dim_;
            float* atth = s->att_ + h * p->seq_len_;
            int16_t* kh = kb + (h / kv_mul) * p->head_dim_;
            int16_t* vh = vb + (h / kv_mul) * p->head_dim_;

            attn(s->xb2_ + h * p->head_dim_, atth, qh, kh, vh, p->head_dim_, kv_dim, kv_len);
        }

        // final matmul to get the output of the attention
        // TODO: we're using hb as a temporary storage, hacky
        matmul(s->hb_, s->xb2_, w->wo_[l], nullptr, q_dim, dim, dotprod);

        // residual connection back into x
        for(int32_t i = 0; i < dim; i++) {
            x[i] += s->hb_[i];
        }

        if(!p->norm_par_) {
            // ffn rmsnorm
            rmsnorm(s->xb_, x, w->rms_ffn_weight_[l], dim, p->norm_eps_, p->norm_ln_);
        }

        float* moe_weights = s->exp_ + p->n_experts_;
        int32_t* moe_experts = (int32_t*)moe_weights + (p->n_experts_ac_ ? p->n_experts_ac_ : 1);

        if(p->n_experts_) {
            // moe gate
            matmul(s->exp_, s->xb_, w->moegate_[l], nullptr, dim, p->n_experts_, dotprod);
            moe_gate(moe_weights, moe_experts, s->exp_, p->n_experts_, p->n_experts_ac_);
        } else {
            moe_weights[0] = 1.0f;
            moe_experts[0] = 0;
        }

        // mix self.w2(F.silu(self.w1(x)) * self.w3(x))
        for(int32_t e = 0; e < (p->n_experts_ac_ ? p->n_experts_ac_ : 1); ++e) {
            size_t esize = dim * hidden_dim * (size_t)w->dbits_ / 8;
            matmul(s->hb_, s->xb_, (uint8_t*)w->w1_[l] + moe_experts[e] * esize, nullptr, dim, hidden_dim, dotprod);
            matmul(s->hb2_, s->xb_, (uint8_t*)w->w3_[l] + moe_experts[e] * esize, nullptr, dim, hidden_dim, dotprod);

            if(p->act_gelu_) {
                // GEGLU non-linearity
                for(int32_t i = 0; i < hidden_dim; i++) {
                    s->hb_[i] = gelu(s->hb_[i]) * s->hb2_[i];
                }
            } else {
                // SwiGLU non-linearity
                for(int32_t i = 0; i < hidden_dim; i++) {
                    s->hb_[i] = silu(s->hb_[i]) * s->hb2_[i];
                }
            }

            matmul(s->xb2_, s->hb_, (uint8_t*)w->w2_[l] + moe_experts[e] * esize, nullptr, hidden_dim, dim, dotprod);

            for(int32_t i = 0; i < dim; i++) {
                x[i] += s->xb2_[i] * moe_weights[e];
            }
        }
    }
    if(flags & FF_UPDATE_KV_ONLY) {
        // only update kv cache and don't output logits
        return nullptr;
    }

    // final rmsnorm
    rmsnorm(x, x, w->rms_final_weight_, dim, p->norm_eps_, p->norm_ln_);

    // classifier into logits
    matmul(s->logits_, x, w->wcls_, nullptr, p->dim_, p->vocab_size_, dotprod);

    return s->logits_;
}
} // namespace cplm
