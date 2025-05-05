#ifndef INC_CPLM_MODEL_H_
#define INC_CPLM_MODEL_H_
#include <stdint.h>

//--- ForwardFlags
//---------------------------------------
enum ForwardFlags
{
    FF_UPDATE_NONE = 0,
    FF_UPDATE_KV_ONLY = 1 << 0, // only update kv cache and don't output logits
};

// How many attention sinks to use for rolling buffer
#define CPLM_KV_SINKS (2)

struct Config
{
    int32_t dim_ = 0;          // transformer dimension
    int32_t hidden_dim_ = 0;   // for ffn layers
    int32_t head_dim_ = 0;     // for attention heads; usually dim / n_heads
    int32_t n_layers_ = 0;     // number of layers
    int32_t n_heads_ = 0;      // number of query heads
    int32_t n_kv_heads_ = 0;   // number of key/value heads (can be < query heads because of multiquery)
    int32_t vocab_size_ = 0;   // vocabulary size, usually 256 (byte-level)
    int32_t seq_len_ = 0;      // max sequence length
    float rope_theta_ = 0.0f;  // RoPE theta
    int32_t rotary_dim_ = 0;   // RoPE rotary dimension (elements after that don't get rotated)
    int32_t n_experts_ = 0;    // number of experts for MoE models
    int32_t n_experts_ac_ = 0; // number of active experts for MoE models
    float norm_eps_ = 0.0f;    // epsilon for layer normalization
    bool act_gelu_ = false;    // use GELU activation function
    bool norm_ln_ = false;     // use full LN normalization
    bool norm_par_ = false;    // use parallel MLP/attention by omitting intermediate normalization
    float qkv_clip_ = 0.0f;    // clip qkv values to [-clip, clip]
};

#define CPLM_MAX_LAYERS (64)
#define CPLM_MAX_EXPERTS (16)

struct Weights
{
    int32_t dbits_ = 0; // 4 for gf4, 8 for fp8, 16 for fp16; determines type of void* below

    // token embedding table
    void* token_embedding_table_ = nullptr; // (vocab_size, dim)
    // weights for norms
    float* rms_att_weight_[CPLM_MAX_LAYERS] = {}; // (dim) rmsnorm weights
    float* rms_ffn_weight_[CPLM_MAX_LAYERS] = {}; // (dim)
    // weights for matmuls
    void* wq_[CPLM_MAX_LAYERS] = {}; // (n_heads * head_dim, dim)
    void* wk_[CPLM_MAX_LAYERS] = {}; // (n_kv_heads * head_dim, dim)
    void* wv_[CPLM_MAX_LAYERS] = {}; // (n_kv_heads * head_dim, dim)
    void* wo_[CPLM_MAX_LAYERS] = {}; // (dim, n_heads * head_dim)
    // weights for ffn
    void* w1_[CPLM_MAX_LAYERS] = {}; // (n_experts?, hidden_dim, dim)
    void* w2_[CPLM_MAX_LAYERS] = {}; // (n_experts?, dim, hidden_dim)
    void* w3_[CPLM_MAX_LAYERS] = {}; // (n_experts?, hidden_dim, dim)
    // final norm
    float* rms_final_weight_ = {}; // (dim,)
    // classifier weights for the logits, on the last layer
    void* wcls_ = {};
    // biases for qkv (qwen)
    float* bqkv_[CPLM_MAX_LAYERS] = {}; // ((n_heads + n_kv_heads * 2) * head_dim)
    // moe gate weights (mixtral)
    void* moegate_[CPLM_MAX_LAYERS] = {}; // (n_experts, dim)
};

struct RunState
{
    // current wave of activations
    float* x_ = nullptr;      // activation at current time stamp (dim,)
    float* xb_ = nullptr;     // same, but inside a residual branch (dim,)
    float* xb2_ = nullptr;    // an additional buffer just for convenience (dim,)
    float* hb_ = nullptr;     // buffer for hidden dimension in the ffn (hidden_dim,)
    float* hb2_ = nullptr;    // buffer for hidden dimension in the ffn (hidden_dim,)
    float* he_ = nullptr;     // buffer for hidden dimension in the ffn (n_experts_ac,hidden_dim,)
    float* q_ = nullptr;      // query (dim,)
    float* k_ = nullptr;      // key (dim,)
    float* v_ = nullptr;      // value (dim,)
    float* att_ = nullptr;    // buffer for scores/attention values (n_heads, seq_len)
    float* exp_ = nullptr;    // buffer for MoE computations (n_experts + n_experts_ac * 2)
    float* logits_ = nullptr; // output logits
    // kv cache
    int32_t kvbits_ = 0;    // 8 for fp8, 16 for fp16; determines type of void* below
    void* key_cache_ = nullptr;   // (layer, seq_len, dim)
    void* value_cache_ = nullptr; // (layer, seq_len, dim)
};

struct Transformer
{
    Config config_;   // the hyperparameters of the architecture (the blueprint)
    Weights weights_; // the weights of the model
    RunState state_;  // buffers for the "wave" of activations in the forward pass
    uint64_t n_params_ = 0;
    uint64_t n_bytes_ = 0;
    uint64_t n_bandwidth_ = 0;
    float* (*forward_)(::Transformer* transformer, int32_t token, int32_t pos, uint32_t flags) = nullptr;
};

#endif //INC_CPLM_MODEL_H_
