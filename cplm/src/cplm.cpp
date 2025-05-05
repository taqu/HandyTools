#include "cplm.h"
#ifdef _WIN32
#    include <Windows.h>
#endif
#include <algorithm>
#include <limits>
#include <sstream>

#include <cuda.h>

#include <utf8proc.h>

#include "infer.h"

// new/delete
void* operator new(std::size_t size)
{
    return mi_malloc(size);
}

void* operator new(std::size_t size, std::align_val_t alignment)
{
    return mi_aligned_alloc(static_cast<std::size_t>(alignment), size);
}

void* operator new(std::size_t size, const std::nothrow_t&) noexcept
{
    return mi_malloc(size);
}

void* operator new(std::size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
    return mi_aligned_alloc(static_cast<std::size_t>(alignment), size);
}

// void* operator new(std::size_t /*size*/, void* ptr) noexcept
//{
//     return ptr;
// }

void operator delete(void* ptr) noexcept
{
    mi_free(ptr);
}

void operator delete(void* ptr, std::size_t /*size*/) noexcept
{
    mi_free(ptr);
}

void operator delete(void* ptr, std::align_val_t alignment) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

void operator delete(void* ptr, std::size_t /*size*/, std::align_val_t alignment) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

void operator delete(void* ptr, const std::nothrow_t&) noexcept
{
    mi_free(ptr);
}

void operator delete(void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

// void operator delete(void* ptr, void*) noexcept
//{
//     (void)ptr;
// }

void* operator new[](std::size_t size)
{
    return mi_malloc(size);
}

void* operator new[](std::size_t size, std::align_val_t alignment)
{
    return mi_aligned_alloc(static_cast<std::size_t>(alignment), size);
}

void* operator new[](std::size_t size, const std::nothrow_t&) noexcept
{
    return mi_malloc(size);
}

void* operator new[](std::size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
    return mi_aligned_alloc(static_cast<std::size_t>(alignment), size);
}

// void* operator new[](std::size_t /*size*/, void* ptr) noexcept
//{
//     return ptr;
// }

void operator delete[](void* ptr) noexcept
{
    mi_free(ptr);
}

void operator delete[](void* ptr, std::size_t /*size*/) noexcept
{
    mi_free(ptr);
}

void operator delete[](void* ptr, std::align_val_t alignment) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

void operator delete[](void* ptr, std::size_t /*size*/, std::align_val_t alignment) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

void operator delete[](void* ptr, const std::nothrow_t&) noexcept
{
    mi_free(ptr);
}

void operator delete[](void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
    mi_free_aligned(ptr, static_cast<std::size_t>(alignment));
}

// void operator delete[](void* ptr, void*) noexcept
//{
//     (void)ptr;
// }

extern "C" void* upload_cuda(void* host, size_t size);
extern "C" bool prepare_cuda(struct Transformer* transformer);
extern "C" void terminate_cuda(struct Transformer* transformer);
extern "C" float* forward_cuda(struct Transformer* transformer, int32_t token, int32_t pos, uint32_t flags);
extern "C" void perf_cuda(void);
extern "C" void free_cuda(void* device);

namespace cplm
{
namespace
{
    char* json_skipws(char* json)
    {
        while(*json == ' ' || *json == '\t' || *json == '\n' || *json == '\r') {
            json++;
        }
        return json;
    }

    char* json_string(char* json, const char** res)
    {
        if(*json != '"') {
            return nullptr;
        }
        json++;

        *res = json;
        while(*json != '"') {
            if(*json == 0 || *json == '\\') {
                return nullptr;
            }
            json++;
        }

        *json = '\0';
        return json_skipws(json + 1);
    }

    char* json_array(char* json, s64* res, int32_t size)
    {
        if(*json != '[') {
            return nullptr;
        }
        json = json_skipws(json + 1);

        for(int32_t i = 0; i < size; ++i) {
            char* end;
            res[i] = strtoll(json, &end, 10);
            if(end == json) {
                return nullptr;
            }
            json = json_skipws(end);
            if(*json == ']') {
                return json_skipws(json + 1);
            }
            if(*json != ',') {
                return nullptr;
            }
            json = json_skipws(json + 1);
        }

        if(*json != ']') {
            return nullptr;
        }
        return json_skipws(json + 1);
    }

    static int32_t json_dtype(const char* str, DType* dtype, int32_t* dsize)
    {
        static const struct
        {
            const char* str;
            DType dtype;
            int32_t dsize;
        } dtypes[] = {
            {"F32", dt_f32, 4},
            {"F16", dt_f16, 2},
            {"BF16", dt_bf16, 2},
            {"F8_E5M2", dt_f8e5m2, 1},
            {"F8_E4M3", dt_f8e4m3, 1},
            {"I32", dt_i32, 4},
            {"I16", dt_i16, 2},
            {"I8", dt_i8, 1},
            {"U8", dt_u8, 1},
        };

        for(size_t i = 0; i < sizeof(dtypes) / sizeof(dtypes[0]); ++i) {
            if(strcmp(str, dtypes[i].str) == 0) {
                *dtype = dtypes[i].dtype;
                *dsize = dtypes[i].dsize;
                return 0;
            }
        }

        return -1;
    }

    static bool validate_shape(int32_t dsize, int32_t shape[4], size_t length)
    {
        size_t expected_length = 1;
        int32_t max_elements = INT_MAX;

        for(int32_t i = 0; i < 4; ++i) {
            int32_t dim_ = shape[i] == 0 ? 1 : shape[i];
            if(dim_ < 0 || dim_ > max_elements) {
                return false;
            }

            expected_length *= dim_;
            max_elements /= dim_;
        }

        return expected_length * dsize == length;
    }

    bool operator<(const TokenIndex& x0, const TokenIndex& x1) noexcept
    {
        return strcmp(x0.str_, x1.str_) < 0;
    }

    struct compare_tokenindex
    {
        bool operator()(const TokenIndex& lhs, const TokenIndex& rhs) const
        {
            return strcmp(lhs.str_, rhs.str_) < 0;
        }
    };
    int32_t str_lookup(const char* str, const TokenIndex* sorted_vocab, int32_t vocab_size)
    {
        // efficiently find the perfect match for str in vocab, return its index or -1 if not found
        TokenIndex tok = {str, -1}; // acts as the key to search for
        const TokenIndex* end = sorted_vocab + vocab_size;
        auto it = std::lower_bound(sorted_vocab, end, tok, compare_tokenindex());
        if(it == end || 0 != strcmp(it->str_, str)) {
            return -1;
        } else {
            std::size_t index = std::distance(sorted_vocab, it);
            return sorted_vocab[index].id_;
        }
    }

} // namespace

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
    memcpy(state_, s, sizeof(uint32_t) * SFMT_N32);
    ;
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

#ifdef _WIN32
FileMap::FileMap()
    : filemap_(false)
    , size_(0)
    , mapping_(nullptr)
    , data_(nullptr)
{
}

FileMap::~FileMap()
{
    close();
}

#    if 0
namespace
{
    std::string GetErrorMessage(DWORD id)
    {
    std::string dest;

    LPVOID buffer = nullptr;
    DWORD result = FormatMessageA(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
	nullptr,
        id,
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        reinterpret_cast<char*>(&buffer),
        0,
        nullptr
    );
    	
    if (result > 0 && buffer != nullptr) {
        dest = reinterpret_cast<char*>(buffer);
        LocalFree(buffer);
        
        if (!dest.empty() && dest[dest.size() - 1] == ('\n')) dest.erase(dest.size() - 1);
        if (!dest.empty() && dest[dest.size() - 1] == ('\r')) dest.erase(dest.size() - 1);
    }
    return dest;
}
}
#    endif

bool FileMap::open(const char* path)
{
    assert(nullptr != path);
    HANDLE file = CreateFileA(path, GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if(INVALID_HANDLE_VALUE == file) {
        return false;
    }
    DWORD high = 0;
    DWORD low = GetFileSize(file, &high);
    HANDLE mapping = CreateFileMappingA(file, nullptr, PAGE_WRITECOPY, high, low, nullptr);
    if(nullptr == mapping) {
        CloseHandle(mapping);
        CloseHandle(file);
        return false;
    }
    uint64_t size = high;
    size <<= 32;
    size |= low;
    LPVOID data = MapViewOfFile(mapping, FILE_MAP_COPY, 0, 0, size);
    CloseHandle(file);
    if(nullptr == data) {
        return false;
    }
    close();
    filemap_ = true;
    size_ = size;
    mapping_ = mapping;
    data_ = data;
    return true;
}

bool FileMap::open(uint64_t size, const void* data)
{
    assert(nullptr != data);
    void* tmp = CPLM_MALLOC(size);
    if(nullptr == tmp) {
        return false;
    }
    ::memcpy(tmp, data, size);
    close();
    filemap_ = false;
    size_ = size;
    data_ = tmp;
    return true;
}

void FileMap::close()
{
    if(filemap_) {
        if(nullptr != data_) {
            UnmapViewOfFile(data_);
            data_ = nullptr;
        }
        if(INVALID_HANDLE_VALUE != mapping_) {
            CloseHandle(mapping_);
            mapping_ = nullptr;
        }
    } else {
        CPLM_FREE(data_);
        data_ = nullptr;
    }
    size_ = 0;
}

#else
FileMap::FileMap()
    : size_(0)
    , data_(nullptr)
{
}

FileMap::~FileMap()
{
    close();
}

bool FileMap::open(const char* path)
{
    assert(nullptr != path);
    int32_t fd = open(filename, O_RDONLY);
    if(fd == -1) {
        return false;
    }

    struct stat st;
    if(fstat(fd, &st) != 0) {
        close(fd);
        return false;
    }

    size_t size = st.st_size;
    void* data = mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_PRIVATE, fd, 0);
    if(data == MAP_FAILED) {
        close(fd);
        return false;
    }

#    ifdef __linux__
    // increases readahead buffer size, resulting in faster cold loads
    posix_fadvise(fd, 0, size, POSIX_FADV_SEQUENTIAL);
#    endif
    close(fd); // fd can be closed after mmap returns without invalidating the mapping

    close();
    size_ = size;
    data_ = data;
    return 0;
}

bool FileMap::open(uint64_t size, const void* data)
{
    assert(nullptr != data);
    void* tmp = CPLM_MALLOC(size);
    if(nullptr == tmp) {
        return false;
    }
    ::memcpy(tmp, data, size);
    close();
    filemap_ = false;
    size_ = size;
    data_ = tmp;
    return true;
}

void FileMap::close()
{
    if(filemap_) {
        if(nullptr != data_) {
            munmap(data_, size_);
            data_ = nullptr;
        }
    } else {
        CPLM_FREE(data_);
        data_ = nullptr;
    }
    size_ = 0;
}
#endif

//--- Timer
//---------------------------------------
Timer::Timer()
    : start_(0)
    , duration_(0)
{
}

Timer::~Timer()
{
}

void Timer::start()
{
    LARGE_INTEGER c;
    if(FALSE == QueryPerformanceCounter(&c)) {
        return;
    }
    start_ = c.QuadPart;
}

void Timer::stop()
{
    LARGE_INTEGER c;
    if(FALSE == QueryPerformanceCounter(&c)) {
        return;
    }
    LARGE_INTEGER f;
    if(FALSE == QueryPerformanceFrequency(&f)) {
        duration_ = 0;
        return;
    }
    duration_ = c.QuadPart - start_;
}

double Timer::seconds() const
{
    LARGE_INTEGER f;
    if(FALSE == QueryPerformanceFrequency(&f)) {
        return 0.0;
    }
    int64_t frequency = f.QuadPart;
    if(frequency <= 0) {
        return 0.0;
    }
    return (double)duration_ / frequency;
}

double Timer::milliseconds() const
{
    LARGE_INTEGER f;
    if(FALSE == QueryPerformanceFrequency(&f)) {
        return 0.0;
    }
    int64_t frequency = f.QuadPart;
    if(frequency <= 0) {
        return 0.0;
    }
    return (double)duration_ / frequency * 1000.0;
}

//--- Tensors
//---------------------------------------
Tensors::Tensors()
{
}

Tensors::~Tensors()
{
    tensors_.clear();
    metadata_.clear();
}

bool Tensors::open(const char* path)
{
    assert(nullptr != path);
    if(!fileMap_.open(path)) {
        return false;
    }
    if(!parse(fileMap_.size(), fileMap_())) {
        fileMap_.close();
        return false;
    }
    return true;
}

bool Tensors::open(uint64_t size, const void* data)
{
    assert(nullptr != data);
    if(!fileMap_.open(size, data)) {
        return false;
    }
    if(!parse(fileMap_.size(), fileMap_())) {
        fileMap_.close();
        return false;
    }
    return true;
}

bool Tensors::parse(u64 size, void* data)
{
    if(size < sizeof(uint64_t)) {
        return false;
    }

    uint64_t json_size = *(uint64_t*)data;
    if(json_size == 0 || json_size > size - sizeof(uint64_t)) {
        return false;
    }

    char* json = (char*)data + sizeof(uint64_t);
    void* bytes = (char*)data + sizeof(uint64_t) + json_size;
    size_t bytes_size = size - sizeof(uint64_t) - json_size;

    json[json_size - 1] = 0;

    if(*json != '{') {
        return false;
    }
    json = json_skipws(json + 1);

    while(*json && *json != '}') {
        const char* key;
        json = json_string(json, &key);
        if(!json || *json != ':') {
            return false;
        }
        json = json_skipws(json + 1);

        if(strcmp(key, "__metadata__") == 0) {
            json = parse_metadata(json);
            if(!json) {
                return false;
            }
        } else {
            Tensor tensor = {};
            json = parse_tensor(tensor, bytes, bytes_size, key, json);
            if(!json) {
                return false;
            }
            tensors_.push_back(tensor);
        }

        if(*json != '}' && *json != ',' && *json != '\0') {
            return false;
        }
        json = (*json == ',') ? json_skipws(json + 1) : json;
    }

    return true;
}

void Tensors::close()
{
    tensors_.clear();
    metadata_.clear();
    fileMap_.close();
}

char* Tensors::parse_metadata(char* json)
{
    if(*json != '{') {
        return nullptr;
    }
    json = json_skipws(json + 1);

    while(*json != '}') {
        Metadata metadata = {};
        json = json_string(json, &metadata.key_);
        if(!json || *json != ':') {
            return nullptr;
        }
        json = json_skipws(json + 1);
        json = json_string(json, &metadata.value_);
        if(!json) {
            return nullptr;
        }
        metadata_.push_back(metadata);

        if(*json != '}' && *json != ',') {
            return nullptr;
        }
        json = (*json == ',') ? json_skipws(json + 1) : json;
    }

    return json_skipws(json + 1);
}

char* Tensors::parse_tensor(Tensor& tensor, void* bytes, size_t bytes_size, const char* name, char* json)
{
    tensor.name_ = name;

    if(*json != '{') {
        return nullptr;
    }
    json = json_skipws(json + 1);

    int32_t dsize = 0;

    while(*json != '}') {
        const char* key;
        json = json_string(json, &key);
        if(!json || *json != ':') {
            return nullptr;
        }
        json = json_skipws(json + 1);

        if(strcmp(key, "dtype") == 0) {
            const char* val;
            json = json_string(json, &val);
            if(!json) {
                return nullptr;
            }
            if(json_dtype(val, &tensor.dtype_, &dsize) != 0) {
                return nullptr;
            }
        } else if(strcmp(key, "shape") == 0) {
            int64_t shape[4] = {};
            json = json_array(json, shape, 4);
            if(!json) {
                return nullptr;
            }

            for(int32_t j = 0; j < 4; ++j) {
                if(shape[j] < 0 || shape[j] > INT_MAX) {
                    return nullptr;
                }
                tensor.shape_[j] = (int32_t)shape[j];
            }
        } else if(strcmp(key, "data_offsets") == 0) {
            int64_t offsets[2] = {};
            json = json_array(json, offsets, 2);
            if(!json) {
                return nullptr;
            }

            if(offsets[0] < 0 || offsets[1] <= offsets[0] || static_cast<int64_t>(bytes_size) < offsets[1]) {
                return nullptr;
            }

            tensor.data_ = (char*)bytes + offsets[0];
            tensor.size_ = offsets[1] - offsets[0];
        } else {
            return nullptr;
        }

        if(*json != '}' && *json != ',') {
            return nullptr;
        }
        json = (*json == ',') ? json_skipws(json + 1) : json;
    }

    if(!validate_shape(dsize, tensor.shape_, tensor.size_)) {
        return nullptr;
    }

    return json_skipws(json + 1);
}
size_t Tensors::num_tensors() const
{
    return tensors_.size();
}

const Tensor& Tensors::get_tensor(size_t index) const
{
    return tensors_[index];
}

const void* Tensors::get() const
{
    return fileMap_();
}

Tensor* Tensors::find(const char* name, int32_t layer)
{
    assert(nullptr != name);
#if _WIN32
    int32_t len = _scprintf(name, layer) + 1;
#else
    int32_t len = snprintf(nullptr, 0, name, layer) + 1;
#endif
    if(len <= 0) {
        return nullptr;
    }
    char buffer[128];
    char* key = buffer;
    if(127 < len) {
        key = (char*)CPLM_MALLOC(len + 1);
    }
    snprintf(key, len, name, layer);

    for(size_t i = 0; i < tensors_.size(); ++i) {
        if(strncmp(tensors_[i].name_, key, len) == 0) {
            if(127 < len) {
                CPLM_FREE(key);
            }
            return &tensors_[i];
        }
    }
    if(127 < len) {
        CPLM_FREE(key);
    }
    return nullptr;
}

void* Tensors::get(const char* name, int32_t layer, DType dtype, std::initializer_list<int32_t> shape_list)
{
    assert(nullptr != name);
    Tensor* tensor = find(name, layer);
    if(tensor == nullptr) {
        fprintf(stderr, "FATAL: Tensor not found: %s\n", name);
        return nullptr;
    }
    int32_t shape[4] = {};
    for(size_t i = 0; i < shape_list.size(); i++) {
        shape[i] = shape_list.begin()[i];
    }

    if(tensor->dtype_ != dtype || memcmp(tensor->shape_, shape, sizeof(tensor->shape_)) != 0) {
        fprintf(stderr, "FATAL: Tensor mismatch: %s\n", name);
        fprintf(stderr, "  Expected: dtype=%d shape=[%d,%d,%d,%d]\n", dtype, shape[0], shape[1], shape[2], shape[3]);
        fprintf(stderr, "  Actual:   dtype=%d shape=[%d,%d,%d,%d]\n", tensor->dtype_, tensor->shape_[0], tensor->shape_[1], tensor->shape_[2], tensor->shape_[3]);
        return nullptr;
    }

    return nullptr == tensor->device_? tensor->data_ : tensor->device_;
}

size_t Tensors::num_metadata() const
{
    return metadata_.size();
}

const Metadata& Tensors::get_metadata(size_t index) const
{
    return metadata_[index];
}

const char* Tensors::metadata_find(const char* name)
{
    assert(nullptr != name);
    for(size_t i = 0; i < metadata_.size(); ++i) {
        if(strcmp(metadata_[i].key_, name) == 0) {
            return metadata_[i].value_;
        }
    }
    return nullptr;
}

const char* Tensors::metadata_get(const char* name)
{
    const char* res = metadata_find(name);
    if(nullptr == res) {
        fprintf(stderr, "FATAL: Metadata not found: %s\n", name);
    }
    return res;
}

int32_t Tensors::metadata_get_int32(const char* name, int32_t defaultValue)
{
    const char* str = metadata_get(name);
    if(nullptr == str) {
        return defaultValue;
    }
    char* end = nullptr;
    int32_t x = strtol(str, &end, 10);
    return end != name ? x : defaultValue;
}

int64_t Tensors::metadata_get_int64(const char* name, int64_t defaultValue)
{
    const char* str = metadata_get(name);
    if(nullptr == str) {
        return defaultValue;
    }
    char* end = nullptr;
    int64_t x = strtoll(str, &end, 10);
    return end != name ? x : defaultValue;
}

float Tensors::metadata_get_float(const char* name, float defaultValue)
{
    const char* str = metadata_get(name);
    if(nullptr == str) {
        return defaultValue;
    }
    char* end = nullptr;
    float x = strtof(str, &end);
    return end != name ? x : defaultValue;
}

Tokenizer::Tokenizer()
    : vocab_(nullptr)
    , vocab_scores_(nullptr)
    , sorted_vocab_(nullptr)
    , vocab_size_(0)
    , bos_id_(-1)
    , eos_id_(-1)
    , eot_id_(-1)
    , byte_fallbacks_(-1)
    , byte_pieces_{}
{
}

Tokenizer::~Tokenizer()
{
    terminate();
}

void Tokenizer::initialize(const char* tokens, const float* scores, int32_t bos_id, int32_t eos_id, int32_t vocab_size, int32_t total_length)
{
    vocab_size_ = vocab_size;
    bos_id_ = bos_id;
    eos_id_ = eos_id;
    eot_id_ = -1;

    vocab_ = (const char**)CPLM_MALLOC(vocab_size * sizeof(const char*));
    sorted_vocab_ = (TokenIndex*)CPLM_MALLOC(vocab_size * sizeof(TokenIndex));
    vocab_scores_ = scores;

    assert(tokens[total_length - 1] == '\0');
    int32_t token_offset = 0;
    for(int32_t i = 0; i < vocab_size; ++i) {
        vocab_[i] = tokens + token_offset;
        sorted_vocab_[i].str_ = tokens + token_offset;
        sorted_vocab_[i].id_ = i;

        int32_t token_length = static_cast<int32_t>(strlen(tokens + token_offset));
        assert(token_length <= MAX_TOKEN_LENGTH && token_offset + token_length + 1 <= total_length);
        token_offset += token_length + 1;
    }

    assert(token_offset == total_length);
    std::sort(sorted_vocab_, sorted_vocab_ + vocab_size_, compare_tokenindex());

    byte_fallbacks_ = str_lookup("<0x00>", sorted_vocab_, vocab_size);

    if(0 <= byte_fallbacks_) {
        for(int32_t i = 0; i < 256; ++i) {
            byte_pieces_[i][0] = (char)i;
            byte_pieces_[i][1] = '\0';
        }
    }

    if(eot_id_ < 0) {
        eot_id_ = str_lookup("<|eot_id|>", sorted_vocab_, vocab_size);
    }
    if(eot_id_ < 0) {
        eot_id_ = str_lookup("<|end|>", sorted_vocab_, vocab_size);
    }
    if(eot_id_ < 0) {
        eot_id_ = str_lookup("<|im_end|>", sorted_vocab_, vocab_size);
    }
}

void Tokenizer::terminate()
{
    CPLM_FREE(sorted_vocab_);
    CPLM_FREE(vocab_);
    vocab_scores_ = nullptr;
    vocab_size_ = 0;
    bos_id_ = -1;
    eos_id_ = -1;
    eot_id_ = -1;
    byte_fallbacks_ = -1;
    ::memset(byte_pieces_, 0, sizeof(byte_pieces_));
}

int32_t Tokenizer::bound(int32_t bytes)
{
    return bytes + 3; // +3 for prefix space, ?BOS, ?EOS
}

const char8_t* Tokenizer::decode(int32_t prev_token, int32_t token) const
{
    const char8_t* piece = (const char8_t*)vocab_[token];
    // following BOS token, sentencepiece decoder strips any leading whitespace (see PR #89)
    if(prev_token == bos_id_ && piece[0] == ' ') {
        piece++;
    }
    // return byte piece for byte fallback tokens (<0x00>, <0x01>, etc.)
    if(byte_fallbacks_ >= 0 && (unsigned)(token - byte_fallbacks_) < 256) {
        piece = (const char8_t*)byte_pieces_[token - byte_fallbacks_];
    }
    return piece;
}

std::u8string Tokenizer::decode(int32_t size, const int32_t* tokens) const
{
    std::basic_stringstream<char8_t> ss;
    if(size <= 0) {
        return ss.str();
    }
    int32_t prev = tokens[0];
    int32_t next = tokens[0];
    for(int32_t i = 0; i < size; ++i) {
        next = tokens[i];
        const char8_t* piece = decode(prev, next);
        ss << piece;
        prev = next;
    }
    return ss.str();
}

std::vector<int32_t> Tokenizer::encode(const char8_t* text, uint32_t flags) const
{
    assert(nullptr != text);
    char8_t* text_nfkc = (char8_t*)utf8proc_NFKC((const utf8proc_uint8_t*)text);
    size_t len = strlen((const char*)text_nfkc);
    std::vector<int32_t> tokens;
    tokens.reserve(len);

    // add optional BOS token, if desired
    if((flags & TF_ENCODE_BOS) && 0 <= bos_id_) {
        tokens.push_back(bos_id_);
    }

    // process the raw (UTF-8) byte sequence of the input string
    for(const char8_t* c = text_nfkc; *c != '\0';) {
        char8_t codepoint[5] = {};

        codepoint[0] = *c++;

        if(codepoint[0] == '<') {
            if(*c == '|') {
                // special token, skip until '|>'
                const char8_t* e = c + 1;
                while(*e && !(e[0] == '|' && e[1] == '>')) {
                    e++;
                }
                if(e[0] == '|' && e[1] == '>' && e - c + 3 <= MAX_TOKEN_LENGTH) {
                    // we found the end of the special token, try to encode it as is
                    char8_t special[MAX_TOKEN_LENGTH + 1];
                    memcpy(special, c - 1, e - c + 3);
                    special[e - c + 3] = '\0';

                    int32_t sid = str_lookup((const char*)special, sorted_vocab_, vocab_size_);
                    if(sid != -1) {
                        // we found special codepoint in vocab, add it as a token
                        tokens.push_back(sid);
                        c = e + 2;
                        continue;
                    }
                }
            } else {
                // special token, skip until '>'
                const char8_t* e = c;
                while(*e && e[0] != '>') {
                    e++;
                }
                if(e[0] == '>' && e - c + 2 <= MAX_TOKEN_LENGTH) {
                    // we found the end of the special token, try to encode it as is
                    char special[MAX_TOKEN_LENGTH + 1];
                    memcpy(special, c - 1, e - c + 2);
                    special[e - c + 2] = '\0';

                    int32_t sid = str_lookup(special, sorted_vocab_, vocab_size_);
                    if(sid != -1) {
                        // we found special codepoint in vocab, add it as a token
                        tokens.push_back(sid);
                        c = e + 1;
                        continue;
                    }
                }
            }
        }

        // this byte is a leading byte (11...), so it's a multi-byte UTF8 codepoint
        if((codepoint[0] & 0xC0) == 0xC0) {
            for(int32_t i = 1; i < 4 && (*c & 0xC0) == 0x80; ++i) {
                codepoint[i] = *c++;
            }
        }

        int32_t id = str_lookup((const char*)codepoint, sorted_vocab_, vocab_size_);

        if(id != -1) {
            // we found this codepoint in vocab, add it as a token
            tokens.push_back(id);
        } else if(0 <= byte_fallbacks_) {
            // byte_fallback encoding: just encode each byte as a token
            for(char8_t* fb = codepoint; *fb != '\0'; ++fb) {
                tokens.push_back((uint8_t)*fb + byte_fallbacks_);
            }
        }
    }

    // optimized heap-based merge
    merge_tokens(tokens);

    // add optional EOS token, if desired
    if(flags & TF_ENCODE_EOS) {
        tokens.push_back(eos_id_);
    }

    assert(static_cast<int32_t>(tokens.size()) <= bound(static_cast<int32_t>(strlen((const char*)text_nfkc))));
    mi_free(text_nfkc);
    return tokens;
}

int32_t Tokenizer::find(const char8_t* token) const
{
    return str_lookup((const char*)token, sorted_vocab_, vocab_size_);
}

void Tokenizer::heap_swap(struct Merge* heap, int32_t i, int32_t j)
{
    Merge tmp = heap[i];
    heap[i] = heap[j];
    heap[j] = tmp;
}

void Tokenizer::heap_insert(Merge* heap, int32_t n_heap, Merge merge)
{
    // insert a new element at the end (breaks heap invariant)
    heap[n_heap] = merge;
    n_heap++;
    // bubble up the new element to its correct position
    int32_t i = n_heap - 1;
    while(i > 0 && heap[i].score > heap[(i - 1) / 2].score) {
        heap_swap(heap, i, (i - 1) / 2);
        i = (i - 1) / 2;
    }
}

void Tokenizer::heap_poptop(Merge* heap, int32_t n_heap)
{
    // move the last element to the top (breaks heap invariant)
    n_heap--;
    heap[0] = heap[n_heap];

    // bubble down the new top element to its correct position
    int32_t i = 0;
    while(i * 2 + 1 < n_heap) {
        // find the largest child
        int32_t j = i * 2 + 1;
        if(j + 1 < n_heap && heap[j + 1].score > heap[j].score) {
            j++;
        }
        // if the largest child is smaller than the parent, we're done
        if(heap[j].score <= heap[i].score) {
            break;
        }
        // otherwise, swap the parent and child
        heap_swap(heap, i, j);
        i = j;
    }
}

int32_t Tokenizer::merge_tokens_tryadd(Merge* heap, int32_t n_heap, int32_t lpos, int32_t lid, int32_t rpos, int32_t rid) const
{
    static constexpr size_t BUFFER_SIZE = Tokenizer::MAX_TOKEN_LENGTH * 2 + 1;
    char str_buffer[BUFFER_SIZE];
    strcpy_s(str_buffer, BUFFER_SIZE, vocab_[lid]);
    strcat_s(str_buffer, BUFFER_SIZE, vocab_[rid]);
    int32_t id = str_lookup(str_buffer, sorted_vocab_, vocab_size_);
    if(id != -1) {
        Merge merge = {lpos, lid, rpos, rid, id, vocab_scores_[id]};
        heap_insert(heap, n_heap++, merge);
    }
    return n_heap;
}

void Tokenizer::merge_tokens(std::vector<int32_t>& tokens) const
{
    // create heap for all token merge pairs
    int32_t n_tokens = static_cast<int32_t>(tokens.size());
    Merge* heap = (Merge*)CPLM_MALLOC(2 * n_tokens * sizeof(struct Merge));
    int32_t n_heap = 0;

    // insert all initial pairs
    for(int32_t i = 0; i < n_tokens - 1; i++) {
        n_heap = merge_tokens_tryadd(heap, n_heap, i, tokens[i], i + 1, tokens[i + 1]);
    }

    // merge all pairs
    while(n_heap > 0) {
        struct Merge merge = heap[0];
        heap_poptop(heap, n_heap--);

        if(tokens[merge.lpos] != merge.lid || tokens[merge.rpos] != merge.rid) {
            continue; // this pair was already merged, skip it
        }

        // merge
        tokens[merge.lpos] = merge.resid;
        tokens[merge.rpos] = -1;

        // we might have new pairs to merge
        for(int32_t i = merge.lpos - 1; i >= 0; i--) {
            if(tokens[i] != -1) {
                n_heap = merge_tokens_tryadd(heap, n_heap, i, tokens[i], merge.lpos, merge.resid);
                break;
            }
        }

        for(int32_t i = merge.rpos + 1; i < n_tokens; i++) {
            if(tokens[i] != -1) {
                n_heap = merge_tokens_tryadd(heap, n_heap, merge.lpos, merge.resid, i, tokens[i]);
                break;
            }
        }
    }

    CPLM_FREE(heap);

    // compact tokens
    int32_t nm_tokens = 0;
    for(int32_t i = 0; i < n_tokens; i++) {
        if(tokens[i] != -1) {
            tokens[nm_tokens++] = tokens[i];
        }
    }
    tokens.resize(static_cast<size_t>(nm_tokens));
}

namespace
{
    int32_t sample_argmax(float* logits, int32_t n)
    {
        int32_t max_i = -1;
        float max_p = (std::numeric_limits<float>::lowest)();
        for(int32_t i = 0; i < n; i++) {
            max_i = logits[i] > max_p ? i : max_i;
            max_p = logits[i] > max_p ? logits[i] : max_p;
        }
        return max_i;
    }

    int32_t sample_minp(float* logits, int32_t n, float minp, float temperature, float coin)
    {
        // find max logit; we will use this to derive minp cutoff (in log space), since minp is scale-invariant (wrt softmax)
        float max_logit = (std::numeric_limits<float>::lowest)();
        for(int32_t i = 0; i < n; i++) {
            max_logit = logits[i] > max_logit ? logits[i] : max_logit;
        }

        // exp(logit / temp) <= exp(max_logit / temp) * minp -> logit <= max_logit + log(minp) * temp
        float logit_cutoff = max_logit + logf(minp) * temperature;

        // convert from logits to probabilities in-place while simultaneously doing (unscaled) softmax; we'll rescale later
        float* probs = logits;
        int32_t fallback = 0;
        float cumulative_prob = 0.0f;
        for(int32_t i = 0; i < n; i++) {
            if(logits[i] >= logit_cutoff) {
                probs[i] = expf((logits[i] - max_logit) / temperature);
                cumulative_prob += probs[i];
                fallback = i; // for fallback due to rounding errors
            } else {
                probs[i] = 0.0f;
            }
        }

        // sample from the truncated list
        float r = coin * cumulative_prob;
        float cdf = 0.0f;
        for(int32_t i = 0; i < n; i++) {
            cdf += probs[i];
            if(r < cdf) {
                return i;
            }
        }
        return fallback; // in case of rounding errors
    }
} // namespace

void Sampler::initialize(int32_t vocab_size, uint64_t seed, float temperature, float minp)
{
    vocab_size_ = vocab_size;
    random_.seed(seed);
    temperature_ = temperature;
    minp_ = minp;
}

float Sampler::sample_prob(int32_t idx, float* logits, int32_t size) const
{
    // find max value (for numerical stability)
    float max_val = (std::numeric_limits<float>::lowest)();
    for(int32_t i = 0; i < size; i++) {
        max_val = logits[i] > max_val ? logits[i] : max_val;
    }
    // exp and sum
    float sum = 0.0f;
    for(int32_t i = 0; i < size; i++) {
        sum += expf(logits[i] - max_val);
    }
    // return probability of the given index
    return expf(logits[idx] - max_val) / sum;
}

int32_t Sampler::sample(float* logits) const
{
    if(temperature_ <= 1.0e-7f || 1.0f <= minp_) {
        // greedy argmax sampling: take the token with the highest probability
        return sample_argmax(logits, vocab_size_);
    } else {
        float coin = random_.frand();
        // min-p (cutoff) sampling, clamping the least likely tokens to zero
        return sample_minp(logits, vocab_size_, minp_, temperature_, coin);
    }
}

void Sampler::seed(uint64_t s)
{
    random_.seed(s);
}

int32_t getCudaDeviceCount()
{
    if(CUDA_ERROR_NOT_INITIALIZED == cuInit(0)) {
        return 0;
    }
    int32_t count = 0;
    CUresult res = cuDeviceGetCount(&count);
    return CUDA_SUCCESS == res ? count : 0;
}

Model::Model()
    : cuda_(false)
{
    cuda_ = 0 < getCudaDeviceCount();
}

Model::~Model()
{
    close();
}

bool Model::open(const char* path, int32_t context)
{
    assert(nullptr != path);
    close();
    if(!tensors_.open(path)) {
        return false;
    }
    return open(context);
}

bool Model::open(uint64_t size, const void* data, int32_t context)
{
    assert(nullptr != data);
    close();
    if(!tensors_.open(size, data)) {
        return false;
    }
    return open(context);
}

void Model::close()
{
    if(cuda_) {
        terminate_cuda(&transformer_);
        for(size_t i = 0; i < tensors_.tensors_.size(); ++i) {
            Tensor& tensor = tensors_.tensors_[i];
            if(nullptr != tensor.device_) {
                free_cuda(tensor.device_);
                tensor.device_ = nullptr;
            }
        }
    }else{
        RunState* s = &transformer_.state_;
        CPLM_FREE(s->x_);
        CPLM_FREE(s->xb_);
        CPLM_FREE(s->xb2_);
        CPLM_FREE(s->hb_);
        CPLM_FREE(s->hb2_);
        CPLM_FREE(s->q_);
        CPLM_FREE(s->k_);
        CPLM_FREE(s->v_);
        CPLM_FREE(s->att_);
        CPLM_FREE(s->exp_);
        CPLM_FREE(s->logits_);
        CPLM_FREE(s->key_cache_);
        CPLM_FREE(s->value_cache_);
        s->kvbits_ = 0;
    }

    tensors_.close();
    tokenizer_.terminate();
    transformer_ = {};
}

std::vector<Result> Model::generate(const char8_t* prompt, const Params& params)
{
    std::vector<Result> results;
    for(int32_t i = 0; i < params.sequences_; ++i) {
        results.push_back(generate_one(prompt, params));
    }
    return results;
}

Result Model::generate_one(const char8_t* prompt, const Params& params)
{
    assert(nullptr != prompt);

    sampler_.initialize(transformer_.config_.vocab_size_, params.seed_, params.temperature_, params.minp_);
    Result result = {};
    // encode the (string) prompt into tokens sequence
    std::vector<int32_t> prompt_tokens = tokenizer_.encode(prompt, TF_ENCODE_BOS);
    int32_t num_prompt_tokens = static_cast<int32_t>(prompt_tokens.size());
    if(num_prompt_tokens < 1) {
        fprintf(stderr, "something is wrong, expected at least 1 prompt token\n");
        return result;
    }

    // start the main loop
    uint64_t read_bytes = 0;

    int32_t next;                     // will store the next token in the sequence
    int32_t token = prompt_tokens[0]; // kick off with the first token in the prompt
    int32_t pos = 0;                  // position in the sequence
    std::basic_ostringstream<char8_t> ss;
    // print first prompt token since it won't be decoded
    if(token != tokenizer_.bos_id_) {
        const char8_t* piece = tokenizer_.decode(tokenizer_.bos_id_, token);
        ss << piece;
    }

    Timer timer;
    timer.start();
    float* logits_last = nullptr;
    while(pos < params.steps_ || params.steps_ < 0) {
        // forward the transformer to get logits for the next token
        unsigned flags = pos < num_prompt_tokens - 1 ? FF_UPDATE_KV_ONLY : 0;
        float* logits = transformer_.forward_(&transformer_, token, pos, flags);

        read_bytes += transformer_.n_bandwidth_;
        read_bytes += kvcache_bandwidth(transformer_.state_.kvbits_, pos);
        logits_last = logits;

        // advance the state machine
        if(pos < num_prompt_tokens - 1) {
            // if we are still processing the input prompt, force the next prompt token
            next = prompt_tokens[pos + 1];
        } else {
            // otherwise sample the next token from the logits
            next = sampler_.sample(logits);
            assert(next >= 0);

            // data-dependent terminating condition: the BOS token delimits sequences, EOS token ends the sequence, EOT token ends the turn
            if(next == tokenizer_.bos_id_ || next == tokenizer_.eos_id_ || next == tokenizer_.eot_id_) {
                break;
            }
        }
        pos++;

        // print the token as string, decode it with the Tokenizer object
        const char8_t* piece = tokenizer_.decode(token, next);
        ss << piece;
        token = next;
    }

    timer.stop();

    // fold last token's logits into a hash for validation
    unsigned logits_hash = 0;
    if(logits_last) {
        for(int32_t k = 0; k < transformer_.config_.vocab_size_; ++k) {
            logits_hash = logits_hash * 5 + *(unsigned*)(&logits_last[k]);
        }
    }

    // fprintf(stderr, "# %d tokens: throughput: %.2f tok/s; latency: %.2f ms/tok; bandwidth: %.2f GB/s; total %.3f sec; #%08x\n",
    //         pos,
    //         pos / (double)(end - start) * 1000, (double)(end - start) / pos,
    //         ((double)read_bytes / 1e9) / ((double)(end - start) / 1000),
    //         (double)(end - start) / 1000, logits_hash);

    result.text_ = ss.str();
    result.num_tokens_ = pos;
    result.duration_ = timer.milliseconds();
    result.read_bytes_ = read_bytes;
    result.logits_hash_ = logits_hash;
    return result;
}

const float* Model::forward(int32_t token, int32_t pos, uint32_t flags)
{
    return transformer_.forward_(&transformer_, token, pos, flags);
}

bool Model::open(int32_t context)
{
    get_config(context);
    if(cuda_) {
        transformer_.forward_ = forward_cuda;
        for(size_t i = 0; i < tensors_.tensors_.size(); ++i) {
            Tensor& tensor = tensors_.tensors_[i];
            if(strncmp(tensor.name_, "model.", 6) == 0) {
                tensor.device_ = upload_cuda(tensor.data_, tensor.size_);
            }
        }
    } else {
        transformer_.forward_ = cplm::forward;
    }

    get_weights();
    build_tokenizer();
    transformer_.n_bytes_ = count_bytes("model.", nullptr, &transformer_.n_params_);
    transformer_.n_bandwidth_ = transformer_.n_bytes_ - count_bytes("model.embed.", nullptr, nullptr);
    if(nullptr == tensors_.find("model.output.weight", 0)) {
        transformer_.n_bandwidth_ += tensors_.find("model.embed.weight", 0)->size_;
    }
    if(0 < transformer_.config_.n_experts_) {
        uint64_t mlp = count_bytes("model.layers.", ".mlp.w", nullptr);
        transformer_.n_bandwidth_ -= mlp;
        transformer_.n_bandwidth_ += mlp / transformer_.config_.n_experts_ * transformer_.config_.n_experts_ac_;
    }

    if(cuda_) {
        if(!prepare_cuda(&transformer_)){
            close();
            return false;
        }
    } else {
        if(!prepare()) {
            close();
            return false;
        }
    }
    return true;
}

void Model::get_config(int32_t context)
{
    transformer_.config_.dim_ = tensors_.metadata_get_int32("dim");
    transformer_.config_.hidden_dim_ = tensors_.metadata_get_int32("hidden_dim");
    transformer_.config_.n_layers_ = tensors_.metadata_get_int32("n_layers");
    transformer_.config_.n_heads_ = tensors_.metadata_get_int32("n_heads");
    transformer_.config_.n_kv_heads_ = tensors_.metadata_get_int32("n_kv_heads");
    transformer_.config_.vocab_size_ = tensors_.metadata_get_int32("vocab_size");
    transformer_.config_.head_dim_ = tensors_.metadata_get_int32("head_dim");
    transformer_.config_.seq_len_ = std::clamp(tensors_.metadata_get_int32("max_seq_len"), 1, 4096);

    if(context) {
        transformer_.config_.seq_len_ = context;
    }
    transformer_.config_.rope_theta_ = tensors_.metadata_get_float("rope_theta");
    transformer_.config_.rotary_dim_ = tensors_.metadata_get_int32("rotary_dim");

    transformer_.config_.n_experts_ = tensors_.metadata_get_int32("n_experts", 0);
    transformer_.config_.n_experts_ac_ = tensors_.metadata_get_int32("n_experts_active", 0);

    transformer_.config_.norm_eps_ = tensors_.metadata_get_float("norm_eps", 1.0e-5f);

    const char* act_type = tensors_.metadata_find("act_type");
    transformer_.config_.act_gelu_ = act_type && strcmp(act_type, "gelu") == 0;

    const char* norm_type = tensors_.metadata_find("norm_type");
    transformer_.config_.norm_ln_ = norm_type && strncmp(norm_type, "layernorm", 9) == 0;  // note: we currently don't support layernorm bias
    transformer_.config_.norm_par_ = norm_type && strcmp(norm_type, "layernorm_par") == 0; // note: we currently don't support layernorm bias

    transformer_.config_.qkv_clip_ = tensors_.metadata_get_float("qkv_clip", FLT_MAX);
}

void Model::get_weights()
{
    const char* dtype = tensors_.metadata_get("dtype");

    DType wtype = strcmp(dtype, "gf4") == 0 ? dt_i32 : (strcmp(dtype, "fp8") == 0 ? dt_f8e5m2 : dt_f16);
    int32_t gsize = strcmp(dtype, "gf4") == 0 ? 8 : 1;

    transformer_.weights_.dbits_ = strcmp(dtype, "gf4") == 0 ? 4 : (strcmp(dtype, "fp8") == 0 ? 8 : 16);

    size_t pos;
    transformer_.weights_.token_embedding_table_ = tensors_.get("model.embed.weight", 0, wtype, {transformer_.config_.vocab_size_, transformer_.config_.dim_ / gsize, 0, 0});
    pos = (size_t)transformer_.weights_.token_embedding_table_ - (size_t)tensors_.get();
    printf("token_embedding_table: %zd\n", pos);

    for(int32_t l = 0; l < transformer_.config_.n_layers_; ++l) {
        transformer_.weights_.rms_att_weight_[l] = (float*)tensors_.get("model.layers.%d.attn.norm.weight", l, dt_f32, {transformer_.config_.dim_, 0, 0, 0});

        if(!transformer_.config_.norm_par_) {
            transformer_.weights_.rms_ffn_weight_[l] = (float*)tensors_.get("model.layers.%d.mlp.norm.weight", l, dt_f32, {transformer_.config_.dim_, 0, 0, 0});
        }

        transformer_.weights_.wq_[l] = tensors_.get("model.layers.%d.attn.wq.weight", l, wtype, {transformer_.config_.n_heads_ * transformer_.config_.head_dim_, transformer_.config_.dim_ / gsize, 0, 0});
        transformer_.weights_.wk_[l] = tensors_.get("model.layers.%d.attn.wk.weight", l, wtype, {transformer_.config_.n_kv_heads_ * transformer_.config_.head_dim_, transformer_.config_.dim_ / gsize, 0, 0});
        transformer_.weights_.wv_[l] = tensors_.get("model.layers.%d.attn.wv.weight", l, wtype, {transformer_.config_.n_kv_heads_ * transformer_.config_.head_dim_, transformer_.config_.dim_ / gsize, 0, 0});
        transformer_.weights_.wo_[l] = tensors_.get("model.layers.%d.attn.wo.weight", l, wtype, {transformer_.config_.dim_, transformer_.config_.n_heads_ * transformer_.config_.head_dim_ / gsize, 0, 0});

        if(tensors_.find("model.layers.%d.attn.wqkv.bias", l)) {
            transformer_.weights_.bqkv_[l] = (float*)tensors_.get("model.layers.%d.attn.wqkv.bias", l, dt_f32, {(transformer_.config_.n_heads_ + transformer_.config_.n_kv_heads_ * 2) * transformer_.config_.head_dim_, 0, 0, 0});
        }

        if(transformer_.config_.n_experts_) {
            transformer_.weights_.moegate_[l] = tensors_.get("model.layers.%d.moegate.weight", l, wtype, {transformer_.config_.n_experts_, transformer_.config_.dim_ / gsize, 0, 0});

            transformer_.weights_.w1_[l] = tensors_.get("model.layers.%d.mlp.w1.weight", l, wtype, {transformer_.config_.n_experts_, transformer_.config_.hidden_dim_, transformer_.config_.dim_ / gsize, 0});
            transformer_.weights_.w2_[l] = tensors_.get("model.layers.%d.mlp.w2.weight", l, wtype, {transformer_.config_.n_experts_, transformer_.config_.dim_, transformer_.config_.hidden_dim_ / gsize, 0});
            transformer_.weights_.w3_[l] = tensors_.get("model.layers.%d.mlp.w3.weight", l, wtype, {transformer_.config_.n_experts_, transformer_.config_.hidden_dim_, transformer_.config_.dim_ / gsize, 0});
        } else {
            transformer_.weights_.w1_[l] = tensors_.get("model.layers.%d.mlp.w1.weight", l, wtype, {transformer_.config_.hidden_dim_, transformer_.config_.dim_ / gsize, 0, 0});
            transformer_.weights_.w2_[l] = tensors_.get("model.layers.%d.mlp.w2.weight", l, wtype, {transformer_.config_.dim_, transformer_.config_.hidden_dim_ / gsize, 0, 0});
            transformer_.weights_.w3_[l] = tensors_.get("model.layers.%d.mlp.w3.weight", l, wtype, {transformer_.config_.hidden_dim_, transformer_.config_.dim_ / gsize, 0, 0});
        }
    }

    transformer_.weights_.rms_final_weight_ = (float*)tensors_.get("model.norm.weight", 0, dt_f32, {transformer_.config_.dim_, 0, 0, 0});

    if(tensors_.find("model.output.weight", 0) == nullptr) {
        printf("tied weights\n");
        transformer_.weights_.wcls_ = transformer_.weights_.token_embedding_table_; // tied weights
    } else {
        printf("not tied weights\n");
        transformer_.weights_.wcls_ = tensors_.get("model.output.weight", 0, wtype, {transformer_.config_.vocab_size_, transformer_.config_.dim_ / gsize, 0, 0});
    }
}

void Model::build_tokenizer()
{
    Tensor* tensor = tensors_.find("tokenizer.tokens", 0);

    char* tokens = (char*)tensors_.get("tokenizer.tokens", 0, dt_u8, {tensor->shape_[0], 0, 0, 0});
    float* scores = (float*)tensors_.get("tokenizer.scores", 0, dt_f32, {transformer_.config_.vocab_size_, 0, 0, 0});

    int32_t bos_id = tensors_.metadata_get_int32("bos_token_id", -1);
    int32_t eos_id = tensors_.metadata_get_int32("eos_token_id", -1);

    tokenizer_.initialize(tokens, scores, bos_id, eos_id, transformer_.config_.vocab_size_, tensor->shape_[0]);
}

bool Model::prepare()
{
    const Config* p = &transformer_.config_;
    RunState* s = &transformer_.state_;
    s->kvbits_ = 16;

    int32_t q_dim = p->head_dim_ * p->n_heads_;
    int32_t kv_dim = p->head_dim_ * p->n_kv_heads_;

    // we calloc instead of malloc to keep valgrind happy
    s->x_ = (float*)mi_calloc(p->dim_, sizeof(float));
    s->xb_ = (float*)mi_calloc(p->dim_, sizeof(float));
    s->xb2_ = (float*)mi_calloc(p->dim_, sizeof(float));
    s->hb_ = (float*)mi_calloc(p->hidden_dim_, sizeof(float));
    s->hb2_ = (float*)mi_calloc(p->hidden_dim_, sizeof(float));
    s->q_ = (float*)mi_calloc(q_dim, sizeof(float));
    s->k_ = (float*)mi_calloc(kv_dim, sizeof(float));
    s->v_ = (float*)mi_calloc(kv_dim, sizeof(float));
    s->att_ = (float*)mi_calloc(p->n_heads_ * p->seq_len_, sizeof(float));
    s->exp_ = (float*)mi_calloc(p->n_experts_ + (p->n_experts_ac_ ? p->n_experts_ac_ : 1) * 2, sizeof(float));
    s->logits_ = (float*)mi_calloc(p->vocab_size_, sizeof(float));
    assert(s->kvbits_ == static_cast<int32_t>(sizeof(int16_t) * 8));
    s->key_cache_ = mi_calloc((size_t)p->n_layers_ * p->seq_len_ * kv_dim, sizeof(int16_t));
    s->value_cache_ = mi_calloc((size_t)p->n_layers_ * p->seq_len_ * kv_dim, sizeof(int16_t));

    // ensure all mallocs went fine
    if(!s->x_ || !s->xb_ || !s->xb2_ || !s->hb_ || !s->hb2_ || !s->q_ || !s->key_cache_ || !s->value_cache_ || !s->att_ || !s->logits_) {
        fprintf(stderr, "malloc failed!\n");
        return false;
    }
    return true;
}

uint64_t Model::count_bytes(const char* prefix, const char* filter, uint64_t* out_params)
{
    uint64_t bytes = 0;
    uint64_t params = 0;
    for(size_t i = 0; i < tensors_.num_tensors(); ++i) {
        const Tensor& tensor = tensors_.get_tensor(i);
        if(strncmp(tensor.name_, prefix, strlen(prefix)) != 0) {
            continue;
        }
        if(filter && strstr(tensor.name_, filter) == nullptr) {
            continue;
        }
        int32_t elts = tensor.dtype_ == dt_i32 ? 8 : 1; // gsize hack for gf4
        for(int32_t j = 0; j < 4 && tensor.shape_[j] != 0; ++j) {
            elts *= tensor.shape_[j];
        }
        params += elts;
        bytes += tensor.size_;
    }
    if(out_params) {
        *out_params = params;
    }
    return bytes;
}

uint64_t Model::kvcache_bandwidth(int32_t kvbits, int32_t pos)
{
    int32_t kv_dim = transformer_.config_.head_dim_ * transformer_.config_.n_kv_heads_;
    int32_t kv_len = pos >= transformer_.config_.seq_len_ ? transformer_.config_.seq_len_ : pos + 1;
    return 2 * (uint64_t)(kvbits / 8) * transformer_.config_.n_layers_ * kv_dim * kv_len;
}

} // namespace cplm
