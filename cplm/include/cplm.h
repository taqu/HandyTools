#ifndef INC_CPLM_H_
#define INC_CPLM_H_
#include <cassert>
#include <cstdint>
#ifdef _WIN32
#include <Windows.h>
#endif
#include <mimalloc.h>
#include <vector>
#include <string>
#include <initializer_list>
#include "model.h"

// new/delete
void* operator new(std::size_t size);
void* operator new(std::size_t size, std::align_val_t alignment);
void* operator new(std::size_t size, const std::nothrow_t&) noexcept;
void* operator new(std::size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept;
// void* operator new(std::size_t size, void* ptr) noexcept;
void operator delete(void* ptr) noexcept;
void operator delete(void* ptr, std::size_t size) noexcept;
void operator delete(void* ptr, std::align_val_t alignment) noexcept;
void operator delete(void* ptr, std::size_t size, std::align_val_t alignment) noexcept;
void operator delete(void* ptr, const std::nothrow_t&) noexcept;
void operator delete(void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept;
// void operator delete(void* ptr, void*) noexcept;

void* operator new[](std::size_t size);
void* operator new[](std::size_t size, std::align_val_t alignment);
void* operator new[](std::size_t size, const std::nothrow_t&) noexcept;
void* operator new[](std::size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept;
// void* operator new[](std::size_t size, void* ptr) noexcept;
void operator delete[](void* ptr) noexcept;
void operator delete[](void* ptr, std::size_t size) noexcept;
void operator delete[](void* ptr, std::align_val_t alignment) noexcept;
void operator delete[](void* ptr, std::size_t size, std::align_val_t alignment) noexcept;
void operator delete[](void* ptr, const std::nothrow_t&) noexcept;
void operator delete[](void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept;
// void operator delete[](void* ptr, void*) noexcept;

#ifndef CPLM_MALLOC
#    define CPLM_MALLOC(size) mi_malloc(size)
#endif

#ifndef CPLM_FREE
#    define CPLM_FREE(ptr) \
        mi_free(ptr); \
        (ptr) = nullptr
#endif

#ifndef CPLM_NEW
#    define CPLM_NEW new
#endif

#ifndef CPLM_PLACEMENT_NEW
#    define CPLM_PLACEMENT_NEW(ptr) new(ptr)
#endif

#ifndef CPLM_DELETE
#    define CPLM_DELETE(ptr) \
        delete(ptr); \
        (ptr) = nullptr
#endif

#ifndef CPLM_DELETE_ARRAY
#    define CPLM_DELETE_ARRAY(ptr) \
        delete[](ptr); \
        (ptr) = nullptr
#endif

namespace gguf
{
class GGUF;
}

namespace cplm
{
using s8 = int8_t;
using s16 = int16_t;
using s32 = int32_t;
using s64 = int64_t;
using u8 = uint8_t;
using u16 = uint16_t;
using u32 = uint32_t;
using u64 = uint64_t;

using f32 = float;
using f64 = double;

struct Random
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

    Random(uint32_t s=1234);
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

//--- FileMap
//---------------------------------------
class FileMap
{
public:
    FileMap();
    ~FileMap();
    bool open(const char* path);
    bool open(uint64_t size, const void* data);
    void close();

    uint64_t size() const
    {
        return size_;
    }
    const void* operator()() const
    {
        return data_;
    }
    void* operator()()
    {
        return data_;
    }

    operator const void*() const
    {
        return data_;
    }
    operator void*()
    {
        return data_;
    }

private:
    FileMap(const FileMap&) = delete;
    FileMap& operator=(const FileMap&) = delete;
    bool filemap_;
    uint64_t size_;
#ifdef _WIN32
    HANDLE mapping_;
    LPVOID data_;
#else
    void* data_;
#endif
};

//--- Timer
//---------------------------------------
class Timer
{
public:
    Timer();
    ~Timer();
    void start();
    void stop();
    double seconds() const;
    double milliseconds() const;
private:
    int64_t start_;
    int64_t duration_;
};

//--- DType
//---------------------------------------
enum DType
{
    dt_f32 = 0,
    dt_f16,
    dt_bf16,
    dt_f8e5m2,
    dt_f8e4m3,
    dt_i32,
    dt_i16,
    dt_i8,
    dt_u8,
};

//--- Tensor
//---------------------------------------
struct Tensor
{
    const char* name_;
    DType dtype_;
    s32 shape_[4];
    u64 size_;
    void* data_;
    void* device_;
};

//--- Metadata
//---------------------------------------
struct Metadata
{
    const char* key_;
    const char* value_;
};

//--- Tensors
//---------------------------------------
class Tensors
{
public:
    Tensors();
    ~Tensors();
    bool open(const char* path);
    bool open(uint64_t size, const void* data);
    void close();

    size_t num_tensors() const;
    const Tensor& get_tensor(size_t index) const;
    const void* get() const;
    Tensor* find(const char* name, int32_t layer);
    void* get(const char* name, int32_t layer, DType dtype, std::initializer_list<int32_t> shape);

    size_t num_metadata() const;
    const Metadata& get_metadata(size_t index) const;
    const char* metadata_find(const char* name);
    const char* metadata_get(const char* name);
    int32_t metadata_get_int32(const char* name, int32_t defaultValue = 0);
    int64_t metadata_get_int64(const char* name, int64_t defaultValue = 0);
    float metadata_get_float(const char* name, float defaultValue = 0.0f);

private:
    friend class Model;
    Tensors(const Tensors&) = delete;
    Tensors& operator=(const Tensors&) = delete;

    bool parse(u64 size, void* data);
    char* parse_metadata(char* json);
    char* parse_tensor(Tensor& tensor, void* bytes, size_t bytes_size, const char* name, char* json);

    FileMap fileMap_;
    std::vector<Metadata> metadata_;
    std::vector<Tensor> tensors_;
};

//--- TokenIndex
//---------------------------------------
struct TokenIndex
{
    const char* str_;
    int32_t id_;
};

//--- TokenizerFlags
//---------------------------------------
enum TokenizerFlags
{
    TF_ENCODE_NONE = 0,
    TF_ENCODE_BOS = 1 << 0,
    TF_ENCODE_EOS = 1 << 1,
};

//--- Tokenizer
//---------------------------------------
class Tokenizer
{
public:
    struct Merge
    {
        int32_t lpos, lid;
        int32_t rpos, rid;
        int32_t resid;
        float score;
    };

    inline static constexpr int32_t MAX_TOKEN_LENGTH = 512;
    Tokenizer();
    ~Tokenizer();

    void initialize(const char* tokens, const float* scores, int32_t bos_id, int32_t eos_id, int32_t vocab_size, int32_t total_length);
    void terminate();

    static int32_t bound(int32_t bytes);

    const char8_t* decode(int32_t prev_token, int32_t token) const;
    std::u8string decode(int32_t size, const int32_t* tokens) const;
    std::vector<int32_t> encode(const char8_t* text, uint32_t flags) const;
    int32_t find(const char8_t* token) const;
private:
    Tokenizer(const Tokenizer&) = delete;
    Tokenizer& operator=(const Tokenizer&) = delete;

    friend class Model;

    static void heap_swap(Merge* heap, int32_t i, int32_t j);
    static void heap_insert(Merge* heap, int32_t n_heap, Merge merge);
    static void heap_poptop(Merge* heap, int32_t n_heap);
    int32_t merge_tokens_tryadd(Merge* heap, int32_t n_heap, int32_t lpos, int32_t lid, int32_t rpos, int32_t rid) const;
    void merge_tokens(std::vector<int32_t>& tokens) const;

    const char** vocab_;
    const float* vocab_scores_;
    TokenIndex* sorted_vocab_;

    int32_t vocab_size_;
    int32_t bos_id_;
    int32_t eos_id_;
    int32_t eot_id_;
    int32_t byte_fallbacks_;
    char byte_pieces_[256][2];
};

//--- Sampler
//---------------------------------------
class Sampler
{
public:
    Sampler() = default;
    ~Sampler() = default;
    void initialize(int32_t vocab_size, uint64_t seed, float temperature, float minp);
    float sample_prob(int32_t idx, float* logits, int32_t size) const;
    int32_t sample(float* logits) const;
    void seed(uint64_t s);
private:
    Sampler(const Sampler&) = delete;
    Sampler& operator=(const Sampler&) = delete;

    int32_t vocab_size_ = 0;
    mutable Random random_;
    float temperature_ = 1.0f;
    float minp_ = 0.1f;
};

// CUDA devices
int32_t getCudaDeviceCount();
}


namespace cplm
{
//--- Result
//---------------------------------------
struct Result
{
    std::u8string text_;
    int32_t num_tokens_;
    double duration_;
    uint64_t read_bytes_;
    uint32_t logits_hash_;
};

//--- Model
//---------------------------------------
class Model
{
public:
    struct Params
    {
        int32_t context_ = 0;
        uint64_t seed_ = 0;
        float temperature_ = 1.0f;
        float minp_ = 0.1f;
        int32_t steps_ = 256;
        int32_t sequences_ = 1;
    };
    Model();
    ~Model();
    bool open(const char* path, int32_t context);
    bool open(uint64_t size, const void* data, int32_t context);
    void close();

    const Tensors& get_tensors() const
    {
        return tensors_;
    }
    const Tokenizer& get_tokenizer() const
    {
        return tokenizer_;
    }

    const Sampler& get_sampler() const
    {
        return sampler_;
    }

    std::vector<Result> generate(const char8_t* prompt, const Params& params);
    Result generate_one(const char8_t* prompt, const Params& params);
    const float* forward(int32_t token, int32_t pos, uint32_t flags);
private:
    Model(const Model&) = delete;
    Model& operator=(const Model&) = delete;
    bool open(int32_t context);
    void get_config(int32_t context);
    void get_weights();
    void build_tokenizer();
    bool prepare();
    uint64_t count_bytes(const char* prefix, const char* filter, uint64_t* out_params);
    uint64_t kvcache_bandwidth(int32_t kvbits, int32_t pos);

    bool cuda_;
    Tensors tensors_;
    ::Transformer transformer_;
    Tokenizer tokenizer_;
    Sampler sampler_;
};
} // namespace cplm
#endif // INC_CPLM_H_
