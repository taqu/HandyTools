#include "model.h"
#include "ggml-backend.h"
#include <cassert>
#include <cstring>
#include <vector>
#include <string>

namespace llama
{
//--- Random
//-------------------------------------
Random::Random(uint32_t s)
{
    index_ = 0;
    seed(s);
}

void Random::seed(uint32_t s)
{
    state_[0] = s;
    for(int32_t i = 1; i < SFMT_N32; ++i) {
        state_[i] = (uint32_t)(1812433253U * (state_[i - 1] ^ (state_[i - 1] >> 30)) + i);
    }
    index_ = SFMT_N32;
    period_certification();
}

void Random::seed(uint64_t s)
{
    uint64_t* span = (uint64_t*)state_;
    span[0] = s & 0xFFFFFFFFUL;
    for(int32_t i = 1; i < SFMT_N64; ++i) {
        uint64_t z = span[i - 1] + 0x9e3779b97f4a7c15UL;
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
        span[i] = z ^ (z >> 31);
    }
    index_ = SFMT_N32;
    period_certification();
}

void Random::seed(uint32_t s[SFMT_N32])
{
    ::memcpy(state_, s, sizeof(uint32_t) * SFMT_N32);
    index_ = SFMT_N32;
    period_certification();
}

uint32_t Random::rand()
{
    if(SFMT_N32 <= index_) {
        generate();
        index_ = 1;
        return state_[0];
    } else {
        return state_[index_++];
    }
}

float Random::frand()
{
    uint32_t u = rand();
    return (u >> 8) / 16777216.0f;
}

void Random::check_modification(int32_t i, uint32_t parity)
{
    uint32_t work = 1;
    for(int32_t j = 0; j < 32; ++j) {
        if((work & parity) != 0) {
            state_[i] ^= work;
            return;
        }
        work = work << 1;
    }
}

void Random::period_certification()
{
    uint32_t inner = 0;
    {
        inner ^= state_[0] & SFMT_PARITY1;
        inner ^= state_[1] & SFMT_PARITY2;
        inner ^= state_[2] & SFMT_PARITY3;
        inner ^= state_[3] & SFMT_PARITY4;
    }
    for(int32_t i = 16; 0 < i; i >>= 1) {
        inner ^= inner >> i;
    }
    inner &= 1;
    if(inner == 1) {
        return;
    }
    check_modification(0, SFMT_PARITY1);
    check_modification(1, SFMT_PARITY2);
    check_modification(2, SFMT_PARITY3);
    check_modification(3, SFMT_PARITY4);
}

void Random::generate()
{
    const int32_t SL2_x8 = SFMT_SL2 * 8;
    const int32_t SR2_x8 = SFMT_SR2 * 8;
    const int32_t SL2_ix8 = 64 - SFMT_SL2 * 8;
    const int32_t SR2_ix8 = 64 - SFMT_SR2 * 8;

    int32_t a = 0;
    int32_t b = SFMT_POS1 * 4;
    int32_t c = (SFMT_N - 2) * 4;
    int32_t d = (SFMT_N - 1) * 4;
    do {
        uint64_t xh = ((uint64_t)state_[a + 3] << 32) | state_[a + 2];
        uint64_t xl = ((uint64_t)state_[a + 1] << 32) | state_[a + 0];
        uint64_t yh = xh << (SL2_x8) | xl >> (SL2_ix8);
        uint64_t yl = xl << (SL2_x8);
        xh = ((uint64_t)state_[c + 3] << 32) | state_[c + 2];
        xl = ((uint64_t)state_[c + 1] << 32) | state_[c + 0];
        yh ^= xh >> (SR2_x8);
        yl ^= xl >> (SR2_x8) | xh << (SR2_ix8);

        state_[a + 3] = state_[a + 3] ^ ((state_[b + 3] >> SFMT_SR1) & SFMT_MSK4) ^ (state_[d + 3] << SFMT_SL1) ^ ((uint32_t)(yh >> 32));
        state_[a + 2] = state_[a + 2] ^ ((state_[b + 2] >> SFMT_SR1) & SFMT_MSK3) ^ (state_[d + 2] << SFMT_SL1) ^ ((uint32_t)yh);
        state_[a + 1] = state_[a + 1] ^ ((state_[b + 1] >> SFMT_SR1) & SFMT_MSK2) ^ (state_[d + 1] << SFMT_SL1) ^ ((uint32_t)(yl >> 32));
        state_[a + 0] = state_[a + 0] ^ ((state_[b + 0] >> SFMT_SR1) & SFMT_MSK1) ^ (state_[d + 0] << SFMT_SL1) ^ ((uint32_t)yl);

        c = d;
        d = a;
        a += 4;
        b += 4;
        if(SFMT_N32 <= b) {
            b = 0;
        }
    } while(a < SFMT_N32);
}

//--- Model
//-------------------------------------
Model* Model::load(const char8_t* path, int32_t n_gpu_layers)
{
    assert(nullptr != path);
    ggml_backend_dev_t device = nullptr;
    for(size_t i = 0; i < ggml_backend_dev_count(); ++i) {
        device = ggml_backend_dev_get(i);
        enum ggml_backend_dev_type type = ggml_backend_dev_type(device);
        if(type == GGML_BACKEND_DEVICE_TYPE_GPU) {
            break;
        }
    }
    if(nullptr == device) {
        for(size_t i = 0; i < ggml_backend_dev_count(); ++i) {
            ggml_backend_dev_t dev = ggml_backend_dev_get(i);
            enum ggml_backend_dev_type type = ggml_backend_dev_type(dev);
            if(type == GGML_BACKEND_DEVICE_TYPE_CPU) {
                device = dev;
                break;
            }
        }
        if(nullptr == device) {
            return nullptr;
        }
    }

    ggml_backend_dev_t devices[] = {device,nullptr};
    llama_model_params model_params = llama_model_default_params();
    model_params.n_gpu_layers = n_gpu_layers;
    model_params.devices = devices;
    model_params.use_mmap = true;
    llama_model* lmodel = llama_model_load_from_file((const char*)path, model_params);
    if(nullptr == lmodel) {
        return nullptr;
    }
    llama_sampler_chain_params sampler_chain_params = llama_sampler_chain_default_params();
    sampler_chain_params.no_perf = true;
    llama_sampler* sampler = llama_sampler_chain_init(sampler_chain_params);
    if(nullptr == sampler) {
        llama_model_free(lmodel);
        return nullptr;
    }
    Model* model = new Model();
    if(nullptr == model){
        return nullptr;
    }
    model->model_ = lmodel;
    model->sampler_ = sampler;
    return model;
}

Model::Model()
    : model_(nullptr)
    , sampler_(nullptr)
{
}

Model::~Model()
{
    if(nullptr != sampler_) {
        llama_sampler_free(sampler_);
        sampler_ = nullptr;
    }
    if(nullptr != model_) {
        llama_model_free(model_);
        model_ = nullptr;
    }
}

Context* Model::begin(const char8_t* prompt, int32_t n_predict, float temperature, uint32_t seed)
{
    assert(nullptr != model_);
    assert(nullptr != prompt);
    assert(0 < n_predict);
    assert(0.0f <= temperature && temperature <= 1.0f);
    llama_sampler_reset(sampler_);
    llama_sampler_chain_add(sampler_, llama_sampler_init_temp(temperature));
    llama_sampler_chain_add(sampler_, llama_sampler_init_dist(seed));

    size_t prompt_size = ::strlen((const char*)prompt);
    const llama_vocab* vocab = llama_model_get_vocab(model_);
    const int32_t n_prompt = -llama_tokenize(vocab, (const char*)prompt, prompt_size, nullptr, 0, true, true);
    llama_token* prompt_tokens = (llama_token*)::malloc(sizeof(llama_token) * n_prompt);
    if(llama_tokenize(vocab, (const char*)prompt, prompt_size, prompt_tokens, n_prompt, true, true) < 0) {
        ::free(prompt_tokens);
        return nullptr;
    }

    llama_context_params ctx_params = llama_context_default_params();
    ctx_params.n_ctx = n_prompt + n_predict - 1;
    ctx_params.n_batch = n_prompt;
    ctx_params.flash_attn = true;
    ctx_params.no_perf = true;
    llama_context* ctx = llama_init_from_model(model_, ctx_params);
    llama_batch batch = llama_batch_get_one(prompt_tokens, n_prompt);

    Context* context = new Context();
    context->context_ = ctx;
    context->num_tokens_ = n_prompt;
    context->prompt_tokens_ = prompt_tokens;
    context->batch_ = batch;
    context->n_prompt_ = n_prompt;
    context->n_predict_ = n_predict;
    return context;
}

void Model::end(Context* context)
{
    if(nullptr == context) {
        return;
    }
    delete context;
}

int32_t Model::generate(int32_t size, char8_t* output, Context* context)
{
    assert(0<size);
    assert(nullptr != context);
    int32_t n_decode = 0;
    llama_token new_token_id;
    llama_batch& batch = context->batch_;
    const llama_vocab * vocab = llama_model_get_vocab(model_);
    int32_t len = 0;
    output[0] = u8'\0';

    for(int32_t n_pos = 0; n_pos + batch.n_tokens < context->n_prompt_ + context->n_predict_;) {
        // evaluate the current batch with the transformer model
        if(llama_decode(context->context_, batch)) {
            return -1;
        }

        n_pos += batch.n_tokens;

        // sample the next token
        {
            new_token_id = llama_sampler_sample(sampler_, context->context_, -1);

            // is it an end of generation?
            if(llama_vocab_is_eog(vocab, new_token_id)) {
                break;
            }

            char buf[64];
            int32_t n = llama_token_to_piece(vocab, new_token_id, buf, sizeof(buf)-1, 0, true);
            if(n < 0) {
                return -1;
            }
            n = (std::min)(size-len-1, n);
            buf[n] = '\0';
#ifdef _WIN32
            strcat_s((char*)output, size, buf);
#else
            strcat((char*)output, buf);
#endif
            len += n;
            if(size <= len) {
                break;
            }
            // prepare the next batch with the sampled token
            batch = llama_batch_get_one(&new_token_id, 1);

            n_decode += 1;
        }
    }
    return n_decode;
}

const struct llama_vocab* Model::vocab() const
{
    assert(nullptr != model_);
    return llama_model_get_vocab(model_);
}

//--- Context
//-------------------------------------
Context::Context()
    : context_(nullptr)
    , num_tokens_(0)
    , prompt_tokens_(nullptr)
    , batch_{}
    , n_prompt_(0)
    , n_predict_(0)
{
}

Context::~Context()
{
    if(nullptr != prompt_tokens_) {
        ::free(prompt_tokens_);
        prompt_tokens_ = nullptr;
    }
    num_tokens_ = 0;
    if(nullptr != context_) {
        llama_free(context_);
        context_ = nullptr;
    }
}
} // namespace llama
