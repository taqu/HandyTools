#ifndef INC_LLAMA_MODEL_H_
#define INC_LLAMA_MODEL_H_
#include "llama.h"
#include <cstdint>

namespace llama
{
//--- Random
//-------------------------------------
class Random
{
public:
    inline static constexpr int32_t SFMT_MEXP = 607;
    inline static constexpr int32_t SFMT_N = (SFMT_MEXP / 128 + 1);
    inline static constexpr int32_t SFMT_N8 = SFMT_N * 16;
    inline static constexpr int32_t SFMT_N32 = SFMT_N * 4;
    inline static constexpr int32_t SFMT_N64 = SFMT_N * 2;
    inline static constexpr int32_t SFMT_POS1 = 2;
    inline static constexpr int32_t SFMT_SL1 = 15;
    inline static constexpr int32_t SFMT_SL2 = 3;
    inline static constexpr int32_t SFMT_SR1 = 13;
    inline static constexpr int32_t SFMT_SR2 = 3;
    inline static constexpr uint32_t SFMT_MSK1 = 0xfdff37ffU;
    inline static constexpr uint32_t SFMT_MSK2 = 0xef7f3f7dU;
    inline static constexpr uint32_t SFMT_MSK3 = 0xff777b7dU;
    inline static constexpr uint32_t SFMT_MSK4 = 0x7ff7fb2fU;
    inline static constexpr uint32_t SFMT_PARITY1 = 0x00000001U;
    inline static constexpr uint32_t SFMT_PARITY2 = 0x00000000U;
    inline static constexpr uint32_t SFMT_PARITY3 = 0x00000000U;
    inline static constexpr uint32_t SFMT_PARITY4 = 0x5986f054U;

    Random(uint32_t s = 1234);
    ~Random() = default;

    void seed(uint32_t s);

    void seed(uint64_t s);

    void seed(uint32_t s[SFMT_N32]);

    uint32_t rand();

    float frand();

private:
    Random(const Random&) = delete;
    Random& operator=(const Random&) = delete;
    void check_modification(int32_t i, uint32_t parity);
    void period_certification();
    void generate();
    uint32_t index_;
    uint32_t state_[SFMT_N32];
};

class Context;

//--- Model
//-------------------------------------
class Model
{
public:
    static Model* load(const char8_t* path, int32_t n_gpu_layers);
    ~Model();

    Context* begin(const char8_t* prompt, int32_t n_predict, float temperature = 0.8f, uint32_t seed = LLAMA_DEFAULT_SEED);
    void end(Context* context);
    int32_t generate(Context* context);

private:
    Model(const Model&) = delete;
    Model& operator=(const Model&) = delete;
    Model();

    llama_model* model_;
    llama_sampler* sampler_;
};

//--- Context
//-------------------------------------
class Context
{
public:
    Context();
    ~Context();

private:
    Context(const Context&) = delete;
    Context& operator=(const Context&) = delete;
    friend class Model;
    llama_context* context_;
    size_t num_tokens_;
    llama_token* prompt_tokens_;
    llama_batch batch_;
    int32_t n_prompt_;
    int32_t n_predict_;
};

} // namespace llama
#endif // INC_LLAMA_MODEL_H_
