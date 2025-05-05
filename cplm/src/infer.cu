#include "model.h"

#include <assert.h>
#include <math.h>
#include <stdint.h>
#include <stdio.h>

#include <cooperative_groups.h>

#include "helpers.cuh"

#define CUDA_CHECK(x)                                                                                    \
	do {                                                                                                 \
		cudaError_t err = x;                                                                             \
		if (err != cudaSuccess) {                                                                        \
			fprintf(stderr, "CUDA error in %s at %s:%d: %s (%s=%d)\n", __FUNCTION__, __FILE__, __LINE__, \
			        cudaGetErrorString(err), cudaGetErrorName(err), err);                                \
			abort();                                                                                     \
		}                                                                                                \
	} while (0)

#define PROF_TOKEN(bytes) ((0xCDAFull << 48) | (bytes))

template <typename T>
struct CoopLayer {
	float* rms_att_weight;
	T* wq;
	T* wk;
	T* wv;
	T* wo;
	float* bqkv;

	float* rms_ffn_weight;
	T* moegate;
	T* w1;
	T* w2;
	T* w3;
};

static cudaStream_t stream;

static int32_t coopsms;

static __constant__ CoopLayer<void> cooplayers[CPLM_MAX_LAYERS];

static uint64_t* coopperf;
static uint64_t coopperfbw[16];
static int32_t coopruns;

static void* cuda_devicecopy(void* host, size_t size) {
	void* device = NULL;
	CUDA_CHECK(cudaMalloc(&device, size));
	CUDA_CHECK(cudaMemcpyAsync(device, host, size, cudaMemcpyHostToDevice));
	return device;
}

static void* cuda_devicealloc(size_t size) {
	void* ptr = NULL;
	CUDA_CHECK(cudaMalloc(&ptr, size));
	return ptr;
}

static void cuda_devicefree(void* ptr)
{
	if(NULL == ptr){
		return;
	}
	cudaFree(ptr);
}

static void* cuda_hostalloc(size_t size) {
	void* ptr = NULL;
	CUDA_CHECK(cudaHostAlloc(&ptr, size, 0));
	return ptr;
}

static void cuda_hostfree(void* ptr)
{
	if(NULL == ptr){
		return;
	}
	cudaFreeHost(ptr);
}

extern "C" void* upload_cuda(void* host, size_t size) {
	return cuda_devicecopy(host, size);
}

extern "C" bool prepare_cuda(struct Transformer* transformer) {
	struct Config* config = &transformer->config_;
	struct Weights* weights = &transformer->weights_;
	struct RunState* state = &transformer->state_;

	if (4096<config->seq_len_) {
		state->kvbits_ = 8; // for now use fp8 for larger contexts automatically without explicit control
	}else{
		state->kvbits_ = 16;
	}

	cudaDeviceProp devprop = {};
	CUDA_CHECK(cudaGetDeviceProperties(&devprop, 0));
	assert(devprop.cooperativeLaunch);

	printf("# CUDA: %s, compute %d.%d, %d SMs, %.1f GiB, peak bandwidth %.0f GB/s (ECC %d)\n",
	       devprop.name, devprop.major, devprop.minor, devprop.multiProcessorCount,
	       (double)devprop.totalGlobalMem / (1024 * 1024 * 1024),
	       (double)devprop.memoryClockRate * (devprop.memoryBusWidth / 8) * 2 / 1e6, devprop.ECCEnabled);

	coopsms = devprop.multiProcessorCount;

	if (getenv("CUDA_INJECTION64_PATH")) {
		coopperf = (uint64_t*)cuda_devicealloc(sizeof(uint64_t) * 16);
		CUDA_CHECK(cudaMemset(coopperf, 0, sizeof(uint64_t) * 16));
	}

	CUDA_CHECK(cudaStreamCreate(&stream));

	int32_t dim = config->dim_;
	int32_t hidden_dim = config->hidden_dim_;
	int32_t q_dim = config->head_dim_ * config->n_heads_;
	int32_t kv_dim = config->head_dim_ * config->n_kv_heads_;

	state->x_ = (float*)cuda_devicealloc(dim * sizeof(float));
	if(NULL == state->x_){
		return false;
	}
	state->hb_ = (float*)cuda_devicealloc(hidden_dim * sizeof(float));
	if(NULL == state->hb_){
		return false;
	}
	state->he_ = (float*)cuda_devicealloc(config->n_experts_ac_ * hidden_dim * sizeof(float));
	if(0<config->n_experts_ac_ && NULL == state->he_){
		return false;
	}
	state->q_ = (float*)cuda_devicealloc(q_dim * sizeof(float));
	if(NULL == state->q_){
		return false;
	}
	state->att_ = (float*)cuda_devicealloc(config->n_heads_ * config->seq_len_ * 2 * sizeof(float));
	if(NULL == state->att_){
		return false;
	}

	assert(state->kvbits_ == 8 || state->kvbits_ == 16);
	state->key_cache_ = cuda_devicealloc((size_t)config->n_layers_ * config->seq_len_ * kv_dim * (state->kvbits_ / 8));
	if(NULL == state->key_cache_){
		return false;
	}
	state->value_cache_ = cuda_devicealloc((size_t)config->n_layers_ * config->seq_len_ * kv_dim * (state->kvbits_ / 8));
	if(NULL == state->value_cache_){
		return false;
	}

	// logits are going to be read by the host so we just allocate them in host and write to host directly
	state->logits_ = (float*)cuda_hostalloc(config->vocab_size_ * sizeof(float));
	if(NULL == state->logits_){
		return false;
	}

	CoopLayer<void> layers[CPLM_MAX_LAYERS];
	for (int32_t l = 0; l < config->n_layers_; ++l) {
		layers[l].rms_att_weight = weights->rms_att_weight_[l];
		layers[l].wq = weights->wq_[l];
		layers[l].wk = weights->wk_[l];
		layers[l].wv = weights->wv_[l];
		layers[l].wo = weights->wo_[l];
		layers[l].bqkv = weights->bqkv_[l];

		layers[l].rms_ffn_weight = weights->rms_ffn_weight_[l];
		layers[l].moegate = weights->moegate_[l];
		layers[l].w1 = weights->w1_[l];
		layers[l].w2 = weights->w2_[l];
		layers[l].w3 = weights->w3_[l];
	}

	CUDA_CHECK(cudaMemcpyToSymbol(cooplayers, layers, sizeof(layers)));
	return true;
}

extern "C" void terminate_cuda(struct Transformer* transformer) {
	struct RunState* state = &transformer->state_;

	cuda_hostfree(state->logits_);
	state->logits_ = NULL;

	cuda_devicefree(state->value_cache_);
	state->value_cache_ = NULL;
	cuda_devicefree(state->key_cache_);
	state->key_cache_ = NULL;

	cuda_devicefree(state->att_);
	state->att_ = NULL;
	cuda_devicefree(state->q_);
	state->q_ = NULL;
	cuda_devicefree(state->he_);
	state->he_ = NULL;
	cuda_devicefree(state->hb_);
	state->hb_ = NULL;
	cuda_devicefree(state->x_);
	state->x_ = NULL;

	state->kvbits_ = 0;
	if(NULL != stream){
		cudaStreamDestroy(stream);
		stream = NULL;
	}

    cuda_devicefree(coopperf);
    coopperf = NULL;
}

extern "C" void free_cuda(void* device)
{
	cudaFree(device);
}

template <typename T>
__device__ inline float embed(T* weight, int32_t idx) {
	return float(weight[idx]);
}

__device__ inline float embed(uint32_t* weight, int32_t idx) {
	return gf4_ff(weight[idx / 8], idx % 8);
}

template <typename T>
__global__ static void kernel_embed(float* o, T* weight, int32_t token, int32_t n) {
	int32_t i = blockIdx.x * blockDim.x + threadIdx.x;
	assert(i < n);

	o[i] = embed(weight, token * n + i);
}

template <typename KVT>
__global__ static void kernel_rotate_sink(uint64_t, int32_t kvd, KVT* key_cache, int32_t head_dim, int32_t kv_sink, float theta_log2, int32_t seq_len, int32_t rotary_dim) {
	int32_t i = (blockIdx.x * blockDim.x + threadIdx.x) * 2;
	assert(i < kv_sink * kvd);

	int32_t l = blockIdx.y;

	int32_t j_head = i % head_dim;
	float freq = j_head >= rotary_dim ? 0.f : exp2f(-theta_log2 * (float)j_head / (float)rotary_dim);

	// rotate sink tokens forward to keep pace with non-sink tokens
	float fcr, fci;
	sincosf(freq, &fci, &fcr);

	size_t loff = (size_t)l * seq_len * kvd;
	KVT* kb = key_cache + loff;

	// note: k layout is transposed / tiled to improve attn_score performance
	int32_t t = i / kvd;
	int32_t k = i % kvd;
	int32_t o = t * 16 + seq_len * (k / 16) * 16 + (k % 16);

	float v0 = float(kb[o + 0]);
	float v1 = float(kb[o + 1]);

	float r0 = v0 * fcr - v1 * fci;
	float r1 = v0 * fci + v1 * fcr;

	kb[o + 0] = KVT(r0);
	kb[o + 1] = KVT(r1);
}

__device__ inline float gelu(float x) {
	return 0.5f * x * (1.0f + tanhf(0.797885f * (x + 0.044715f * x * x * x)));
}

__device__ inline float silu(float x) {
	return x / (1.0f + expf(-x));
}

__device__ static void moe_gate_warp(float* moe_weights, int32_t* moe_experts, float* weights, int32_t experts, int32_t active) {
	int32_t i = threadIdx.x;

	// (unscaled) softmax across experts
	float w = (i < experts) ? weights[i] : -FLT_MAX;
	float max_val = warpreduce_max(w);
	w = expf(w - max_val);

	// weight in top 24 bits, index in bottom 8
	int32_t wi = (__float_as_int(w) & 0xffffff00) | i;

	// top k within warp
	float sumw = 0.f;
	int32_t acti = -1;

	for (int32_t k = 0; k < active; ++k) {
		int32_t maxi = warpreduce_maxi(wi);

		sumw += __int_as_float(maxi);

		// keeps top weight in thread k, clears weight for thread with max thread to avoid re-selection
		acti = (i == k) ? maxi : acti;
		wi = (wi == maxi) ? 0 : wi;
	}

	// write normalized weights
	if (i < active) {
		assert(acti >= 0);

		moe_experts[i] = acti & 0xff;
		moe_weights[i] = __int_as_float(acti) / sumw;
	}
}

__device__ inline float4 attn_load4(half* p) {
	ablock<__half2_raw, 2> h = *(ablock<__half2_raw, 2>*)p;
	float2 h0 = __half22float2(h.v[0]), h1 = __half22float2(h.v[1]);
	return {h0.x, h0.y, h1.x, h1.y};
}

__device__ inline float4 attn_load4(__nv_fp8_e5m2* p) {
	return fp8x4_e5m2_ff(*(__nv_fp8x4_e5m2*)p);
}

template <typename KVT>
__device__ inline float attn_score(KVT* kht, float* qh, int32_t head_dim, int32_t seq_len, int32_t t, int32_t off) {
	float score = 0.0f;
	for (int32_t j = 0; j < head_dim; j += 16) {
		float4 kk = attn_load4(&kht[j * seq_len + t * 16 + off]);
		float4 qq = *(float4*)&qh[j + off];
		score += kk.x * qq.x;
		score += kk.y * qq.y;
		score += kk.z * qq.z;
		score += kk.w * qq.w;
	}

	return score;
}

template <typename KVT>
__device__ inline float attn_warpdot(KVT* val, float* atth, int32_t kv_len) {
	int32_t kv_len4 = kv_len & ~3;
	int32_t lane = threadIdx.x % warpSize;

	float res = 0.0f;
	float sum = 0.0f;
	for (int32_t t = lane * 4; t < kv_len4; t += warpSize * 4) {
		float4 vv = attn_load4(&val[t]);
		float4 aa = *(float4*)&atth[t];
		res += vv.x * aa.x;
		res += vv.y * aa.y;
		res += vv.z * aa.z;
		res += vv.w * aa.w;
		sum += aa.x + aa.y + aa.z + aa.w;
	}

	if (kv_len4 + lane < kv_len) {
		float a = atth[kv_len4 + lane];
		res += a * float(val[kv_len4 + lane]);
		sum += a;
	}

	res = warpreduce_sum(res);
	sum = warpreduce_sum(sum);

	return res / sum;
}

__device__ static void softmax(float* xout, float* x, int32_t size) {
	int32_t i = threadIdx.x;

	// find max value per thread (for numerical stability)
	float max_val = -FLT_MAX;
	for (int32_t j = i; j < size; j += blockDim.x) {
		max_val = max(max_val, x[j]);
	}

	// max across threads in block
	max_val = blockreduce_max(max_val);

	// exp per thread
	for (int32_t j = i; j < size; j += blockDim.x) {
		xout[j] = expf(x[j] - max_val);
	}
}

template <typename T>
__device__ static float rmsnorm(T* o, float* x, float* weight, int32_t size, float eps, bool ln) {
	int32_t i = threadIdx.x;
	int32_t blockSize = blockDim.x;

	float mean = 0.0f;
	if (ln) {
		// calculate sum (per thread)
		float sum = 0.0f;
		for (int32_t j = i; j < size; j += blockSize) {
			sum += x[j];
		}

		// sum across threads in block
		mean = blockreduce_sum(sum) / size;
	}

	// calculate sum of squares (per thread)
	float ss = 0.0f;
	for (int32_t j = i * 2; j < size; j += blockSize * 2) {
		float2 xx = *(float2*)&x[j];
		float2 ww = *(float2*)&weight[j];
		float v0 = xx.x - mean;
		float v1 = xx.y - mean;
		ss += v0 * v0;
		ss += v1 * v1;
		*(ablock<T, 2>*)&o[j] = { v0 * ww.x, v1 * ww.y };
	}

	// sum across threads in block
	ss = blockreduce_sum(ss);

	// caller is responsible for normalization
	return rsqrtf(ss / size + eps);
}

__device__ static void syncgrid() {
	volatile uint32_t* barrier = &cooperative_groups::details::get_grid_workspace()->barrier;

	if (threadIdx.x == 0) {
		uint32_t nb = 1;
		if (blockIdx.x == 0) {
			nb = 0x80000000 - (gridDim.x - 1);
		}

		uint32_t old_arrive;
		asm volatile("atom.add.release.gpu.u32 %0,[%1],%2;" : "=r"(old_arrive) : _CG_ASM_PTR_CONSTRAINT(barrier), "r"(nb) : "memory");

		uint32_t current_arrive;
		do {
			asm volatile("ld.acquire.gpu.u32 %0,[%1];" : "=r"(current_arrive) : _CG_ASM_PTR_CONSTRAINT(barrier) : "memory");
		} while (((old_arrive ^ current_arrive) & 0x80000000) == 0);
	}

	__syncthreads();
}

template <typename T, typename KVT>
struct CoopArgs {
	uint64_t bw;
	uint64_t* perfstats;

	float* x;
	float* hb;
	float* q;
	float* att;

	KVT* key_cache;
	KVT* val_cache;

	int32_t n_layers;

	int32_t dim;
	int32_t hidden_dim;
	int32_t head_dim;
	int32_t n_heads;
	int32_t n_kv_heads;
	int32_t n_experts;
	int32_t n_experts_ac;
	int32_t seq_len;
	int32_t rotary_dim;

	bool norm_ln;
	bool act_gelu;

	int32_t kv_len;
	int32_t kv_pos;
	int32_t pos;

	float norm_eps;
	float theta_log2;
	float qkv_clip;
};

__device__ static void coopstage(uint64_t* stats, int32_t stage) {
	__shared__ uint64_t lastt;

	if (stats && blockIdx.x == 0 && threadIdx.x == 0) {
		uint64_t t;
		asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(t));

		if (stage >= 0) {
			stats[stage] += t - lastt;
		}
		lastt = t;
	}
}

template <typename T, typename KVT, typename AT>
__global__ __launch_bounds__(1024, 1) static void kernel_forward(const __grid_constant__ CoopArgs<T, KVT> args) {
	extern __shared__ char smem[];
	__shared__ float rmsscale;

	__shared__ float moe_weights[32];
	__shared__ int32_t moe_experts[32];

	AT* xs = (AT*)smem;

	int32_t dim = args.dim;
	int32_t hidden_dim = args.hidden_dim;
	int32_t head_dim = args.head_dim;

	int32_t kv_mul = args.n_heads / args.n_kv_heads;
	int32_t q_dim = args.head_dim * args.n_heads;
	int32_t kv_dim = args.head_dim * args.n_kv_heads;

	const int32_t IK = 4; // K consecutive warps per block, groups of K are interleaved across SMs for better work distribution
	int32_t io = blockIdx.x * IK + (threadIdx.x / warpSize % IK) + gridDim.x * IK * (threadIdx.x / warpSize / IK);
	int32_t ib = (gridDim.x * blockDim.x) / warpSize;

	// dummy moe weights for non-moe models; will be overwritten by moe gate
	moe_weights[0] = 1.f;
	moe_experts[0] = 0;

	coopstage(args.perfstats, -1); // init timing

	static __device__ int32_t badsoftmax = 0;

	for (int32_t l = 0; l < args.n_layers; ++l) {
		const CoopLayer<T>* L = (const CoopLayer<T>*)&cooplayers[l];

		if (blockIdx.x == 0 && threadIdx.x < warpSize) {
			badsoftmax = 0;
		}

		// pre-attention rmsnorm (into shared memory)
		rmsscale = rmsnorm(xs, args.x, L->rms_att_weight, dim, args.norm_eps, args.norm_ln);

		size_t loff = (size_t)l * args.seq_len * kv_dim; // kv cache layer offset for convenience
		KVT* keyb = args.key_cache + loff;
		KVT* valb = args.val_cache + loff;

		// qkv matmul + RoPE encoding + update KV cache
		for (int32_t j = io * 2; j < q_dim + kv_dim * 2; j += ib * 2) {
			T* w = j < q_dim ? L->wq : (j < q_dim + kv_dim ? L->wk : L->wv);
			int32_t k = j < q_dim ? j : (j < q_dim + kv_dim ? j - q_dim : j - q_dim - kv_dim);

			float v0 = matmul_warppar(xs, w, k + 0, dim) * rmsscale;
			float v1 = matmul_warppar(xs, w, k + 1, dim) * rmsscale;

			if (L->bqkv) {
				v0 += L->bqkv[j + 0];
				v1 += L->bqkv[j + 1];
			}

			v0 = min(max(v0, -args.qkv_clip), args.qkv_clip);
			v1 = min(max(v1, -args.qkv_clip), args.qkv_clip);

			if (threadIdx.x % warpSize == 0) {
				int32_t j_head = j % head_dim;
				float freq = j_head >= args.rotary_dim ? 0.f : exp2f(-args.theta_log2 * (float)j_head / (float)args.rotary_dim);
				float fcr, fci;
				sincosf(args.pos * freq, &fci, &fcr);

				if (j < q_dim) {
					args.q[k + 0] = v0 * fcr - v1 * fci;
					args.q[k + 1] = v0 * fci + v1 * fcr;
				} else if (j < q_dim + kv_dim) {
					// note: k layout is transposed / tiled to improve attn_score performance
					int32_t off = args.kv_pos * 16 + args.seq_len * (k / 16) * 16 + (k % 16);
					keyb[off + 0] = KVT(v0 * fcr - v1 * fci);
					keyb[off + 1] = KVT(v0 * fci + v1 * fcr);
				} else {
					// note: v layout is transposed (we store all positions for a given head contiguously) to improve attn_mix performance
					valb[args.kv_pos + args.seq_len * (k + 0)] = KVT(v0);
					valb[args.kv_pos + args.seq_len * (k + 1)] = KVT(v1);
				}
			}
		}

		__syncthreads(); // TODO: unclear why this is needed for determinism
		syncgrid();
		coopstage(args.perfstats, 0);

		// attention score
		int32_t kv_lent = (args.kv_len + 7) / 8;

		for (int32_t j = io; j < kv_lent * args.n_heads; j += ib) {
			int32_t h = j % args.n_heads;
			int32_t kvh = h / kv_mul;
			int32_t t = (j / args.n_heads) * 8 + (threadIdx.x % warpSize) / 4;

			unsigned active = __ballot_sync(0xffffffff, t < args.kv_len);

			if (t < args.kv_len) {
				float* qh = args.q + h * head_dim;
				KVT* kh = keyb + kvh * head_dim * args.seq_len;
				float* atth = args.att + h * args.seq_len * 2;

				float score = attn_score(kh, qh, head_dim, args.seq_len, t, 4 * (threadIdx.x % 4));

				// reduce score across threads in warp; every 4 threads are processing the same output score
				score += __shfl_xor_sync(active, score, 2);
				score += __shfl_xor_sync(active, score, 1);
				score /= sqrtf(head_dim);

				atth[t] = expf(score);
				atth[t + args.seq_len] = score;

				// to reduce latency we prefer computing softmax without the numeric stabilization, which is safe if all inputs are small
				if (fabsf(score) > 40) {
					badsoftmax = 1;
				}
			}
		}

		syncgrid();
		coopstage(args.perfstats, 1);

		if (badsoftmax) {
			// attention softmax
			if (blockIdx.x < args.n_heads) {
				int32_t h = blockIdx.x;
				float* atth = args.att + h * args.seq_len * 2;

				softmax(atth, atth + args.seq_len, args.kv_len);
			}

			syncgrid();
			coopstage(args.perfstats, 2);
		}

		// attention mix
		for (int32_t j = io; j < q_dim; j += ib) {
			int32_t h = j / head_dim;
			int32_t kvh = h / kv_mul;
			int32_t j_head = j % head_dim;

			float* atth = args.att + h * args.seq_len * 2;
			KVT* vh = valb + kvh * head_dim * args.seq_len;
			KVT* val = vh + j_head * args.seq_len;

			float res = attn_warpdot(val, atth, args.kv_len);

			if (threadIdx.x % warpSize == 0) {
				args.q[j] = res;
			}
		}

		syncgrid();
		coopstage(args.perfstats, 3);

		// attention output
		for (int32_t j = io; j < dim; j += ib) {
			float val = matmul_warppar(args.q, L->wo, j, q_dim);

			if (threadIdx.x % warpSize == 0) {
				args.x[j] += val;
			}
		}

		__syncthreads(); // TODO: unclear why this is needed for determinism
		syncgrid();
		coopstage(args.perfstats, 4);

		// post-attention rmsnorm (into shared memory)
		if (L->rms_ffn_weight) {
			rmsscale = rmsnorm(xs, args.x, L->rms_ffn_weight, dim, args.norm_eps, args.norm_ln);
		}

		// moegate
		if (args.n_experts) {
			__shared__ float exp[32];
			int32_t j = threadIdx.x / warpSize;

			if (j < args.n_experts) {
				float val = matmul_warppar(xs, L->moegate, j, dim) * rmsscale;

				exp[j] = val;
			}

			__syncthreads();

			if (threadIdx.x < warpSize) {
				moe_gate_warp(moe_weights, moe_experts, exp, args.n_experts, args.n_experts_ac);
			}

			__syncthreads();
		}

		// F.silu(self.w1(x)) * self.w3(x)
		for (int32_t j = io; j < hidden_dim * args.n_experts_ac; j += ib) {
			int32_t je = (j % hidden_dim) + moe_experts[j / hidden_dim] * hidden_dim;
			float v1 = matmul_warppar(xs, L->w1, je, dim) * rmsscale;
			float v3 = matmul_warppar(xs, L->w3, je, dim) * rmsscale;

			float val = (args.act_gelu ? gelu(v1) : silu(v1)) * v3;

			if (threadIdx.x % warpSize == 0) {
				args.hb[j] = val;
			}
		}

		syncgrid();
		coopstage(args.perfstats, 5);

		// self.w2(...) + pre-rmsnorm residual
		for (int32_t j = io; j < dim * args.n_experts_ac; j += ib) {
			int32_t je = (j % dim) + moe_experts[j / dim] * dim;
			float val = matmul_warppar(args.hb + (j / dim) * hidden_dim, L->w2, je, hidden_dim);

			if (threadIdx.x % warpSize == 0) {
				atomicAdd(&args.x[j % dim], val * moe_weights[j / dim]);
			}
		}

		__syncthreads(); // TODO: unclear why this is needed for determinism
		syncgrid();
		coopstage(args.perfstats, 6);
	}
}

template <typename T, typename AT>
__global__ static void kernel_output(uint64_t, float* xout, float* x, T* w, float* rms_weight, int32_t n, int32_t d, float norm_eps, bool norm_ln) {
	extern __shared__ char smem[];

	AT* xs = (AT*)smem;

	float rmsscale = rmsnorm(xs, x, rms_weight, n, norm_eps, norm_ln);

	int32_t io = (blockIdx.x * blockDim.x + threadIdx.x) / warpSize;
	int32_t ib = (gridDim.x * blockDim.x) / warpSize;

	for (int32_t j = io; j < d; j += ib) {
		float val = matmul_warppar(xs, w, j, n) * rmsscale;

		// instead of writing one value per block, we transpose the values and write all results from first warp
		val = blocktranspose(val, 0.f);

		if (threadIdx.x < blockDim.x / warpSize) {
			xout[j + threadIdx.x] = val;
		}
	}
}

template <typename T, typename KVT, typename AT = float>
static float* forward(struct Transformer* transformer, int32_t token, int32_t pos, unsigned flags) {
	struct Config* p = &transformer->config_;
	struct Weights* w = &transformer->weights_;
	struct RunState* s = &transformer->state_;

	// a few convenience variables
	float* x = s->x_;
	int32_t dim = p->dim_;
	int32_t hidden_dim = p->hidden_dim_;
	int32_t kv_dim = p->head_dim_ * p->n_kv_heads_;
	size_t dbits = w->dbits_; // size_t prevents integer overflow in multiplications below

	// following "attention sinks" from StreamingLLM we keep the first few tokens in the KV cache as is
	int32_t kv_sink = pos >= p->seq_len_ ? CPLM_KV_SINKS : 0;
	int32_t kv_pos = kv_sink + (pos - kv_sink) % (p->seq_len_ - kv_sink);
	int32_t kv_len = pos >= p->seq_len_ ? p->seq_len_ : pos + 1;

	// ensure all dimensions are warp-aligned
	assert(dim % 32 == 0 && kv_dim % 32 == 0 && hidden_dim % 32 == 0);

	// copy the token embedding into x
	assert(token < p->vocab_size_);
	kernel_embed<<<dim / 32, 32, 0, stream>>>(x, (T*)w->token_embedding_table_, token, dim);

	// rotate sink tokens forward to keep pace with non-sink tokens
	if (kv_sink > 0) {
		kernel_rotate_sink<<<dim3(kv_sink * kv_dim / 64, p->n_layers_), 32, 0, stream>>>(
		    PROF_TOKEN(kv_sink * kv_dim * sizeof(KVT)), kv_dim, (KVT*)s->key_cache_, p->head_dim_, kv_sink, log2(p->rope_theta_), p->seq_len_, p->rotary_dim_);
	}

	// forward all the layers
	size_t kvbw = p->n_kv_heads_ * p->head_dim_ * kv_len * sizeof(KVT) + p->n_heads_ * kv_len * sizeof(float);

	uint64_t bw = 0;
	bw += p->head_dim_ * (p->n_heads_ + p->n_kv_heads_ * 2) * dim * dbits / 8; // QKV
	bw += kvbw * 2; // attn scoring and mixing
	bw += p->head_dim_ * p->n_heads_ * dim * dbits / 8; // attn output
	bw += 3 * (hidden_dim * dim * dbits / 8) * max(p->n_experts_ac_, 1); // MLP
	bw *= p->n_layers_;

	coopruns++;
	coopperfbw[0] += (size_t)p->n_layers_ * (p->head_dim_ * (p->n_heads_ + p->n_kv_heads_ * 2) * dim * dbits / 8); // QKV
	coopperfbw[1] += (size_t)p->n_layers_ * kvbw; // attn scoring
	coopperfbw[2] += 0; // attn softmax
	coopperfbw[3] += (size_t)p->n_layers_ * kvbw; // attn mixing
	coopperfbw[4] += (size_t)p->n_layers_ * (p->head_dim_ * p->n_heads_ * dim * dbits / 8); // attn output
	coopperfbw[5] += (size_t)p->n_layers_ * (2 * (hidden_dim * dim * dbits / 8) * max(p->n_experts_ac_, 1)); // MLP
	coopperfbw[6] += (size_t)p->n_layers_ * (1 * (hidden_dim * dim * dbits / 8) * max(p->n_experts_ac_, 1)); // MLP

	CoopArgs<T, KVT> args = {
		PROF_TOKEN(bw),
		coopperf,
		// token state
		x, p->n_experts_ ? s->he_ : s->hb_, s->q_, s->att_,
		// key/value cache; note that layers are passed via cooplayers[]
		(KVT*)s->key_cache_, (KVT*)s->value_cache_,
		// model dimensions
		p->n_layers_,
		dim, hidden_dim, p->head_dim_,
		p->n_heads_, p->n_kv_heads_, p->n_experts_, max(p->n_experts_ac_, 1),
		p->seq_len_, p->rotary_dim_,
		// model configuration
		p->norm_ln_, p->act_gelu_,
		// token position (and derived data)
		kv_len, kv_pos, pos,
		// model parameters
		p->norm_eps_, log2(p->rope_theta_), p->qkv_clip_,
	};
	void* argsp = &args;

	CUDA_CHECK(cudaLaunchCooperativeKernel((void*)kernel_forward<T, KVT, AT>, coopsms, 1024, &argsp, dim * sizeof(AT), stream));

	if (flags & FF_UPDATE_KV_ONLY) {
		// only update kv cache and don't output logits
		return NULL;
	}

	int32_t output_blk = 32 * 32;
	int32_t output_par = 1;
	CUDA_CHECK(cudaOccupancyMaxActiveBlocksPerMultiprocessor(&output_par, kernel_output<T, AT>, output_blk, dim * sizeof(AT)));

	// classifier into logits
	kernel_output<T, AT><<<coopsms * output_par, output_blk, dim * sizeof(AT), stream>>>(
	    PROF_TOKEN(p->vocab_size_ * dim * dbits / 8), s->logits_, x, (T*)w->wcls_, w->rms_final_weight_, dim, p->vocab_size_, p->norm_eps_, p->norm_ln_);

	CUDA_CHECK(cudaStreamSynchronize(stream));
	CUDA_CHECK(cudaGetLastError()); // check for kernel launch errors; they might fail with OOM due to lazy kernel compilation

	return s->logits_;
}

extern "C" float* forward_cuda(struct Transformer* transformer, int32_t token, int32_t pos, unsigned flags) {
#define CASE(dbits, dtype, kvbits, kvtype, atype)                                   \
	if (transformer->weights_.dbits_ == dbits && transformer->state_.kvbits_ == kvbits) \
	return forward<dtype, kvtype, atype>(transformer, token, pos, flags)

	CASE(4, uint32_t, 8, __nv_fp8_e5m2, float);
	CASE(4, uint32_t, 16, __half, float);
	CASE(8, __nv_fp8_e5m2, 8, __nv_fp8_e5m2, float);
	CASE(8, __nv_fp8_e5m2, 16, __half, float);
	CASE(16, __half, 8, __nv_fp8_e5m2, float);
	CASE(16, __half, 16, __half, float);

	assert(!"Unsupported dbits/kvbits combination for CUDA: dbits must be 4, 8 or 16, kvbits must be 8 or 16");
	return NULL;

#undef CASE
}

extern "C" void perf_cuda() {
	if (coopperf == NULL || coopruns == 0)
		return;

	uint64_t hostperf[16] = {};
	CUDA_CHECK(cudaMemcpy(hostperf, coopperf, sizeof(hostperf), cudaMemcpyDeviceToHost));

	static const char* stagenames[16] = {
	    "matmul_qkv",
	    "attn_score",
	    "attn_softmax",
	    "attn_mix",
	    "matmul_attn",
	    "matmul_ffn_up",
	    "matmul_ffn_down",
	};

	double freq = 1e9;

	uint64_t total = 0;
	for (int32_t stage = 0; stage < 16; ++stage) {
		total += hostperf[stage];
	}

	printf("\nkernel_forward breakdown (over %d runs, avg %.1f usec/run):\n",
	       coopruns, (double)total / (double)coopruns / freq * 1e6);

	for (int32_t stage = 0; stage < 16; ++stage) {
		if (hostperf[stage] == 0)
			continue;

		uint64_t t = hostperf[stage];
		uint64_t tbw = coopperfbw[stage];

		printf("\t[%d] %16s: %4.1f%%; %8.1f usec/run, %6.1f GB/s\n",
		       stage, stagenames[stage],
		       (double)t / (double)total * 100,
		       (double)(t / coopruns) / freq * 1e6,
		       ((double)tbw / 1e9) / ((double)t / freq));
	}
}
