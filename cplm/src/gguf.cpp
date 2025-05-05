#include "gguf.h"
#include <cstdio>
#ifdef _MSC_VER
#    include <Windows.h>
#else
#    include <sys/stat.h>
#    include <sys/types.h>
#    include <unistd.h>
#endif
#include <cstring>
#include <utility>
#ifdef _DEBUG
#    include <iostream>
#endif

namespace gguf
{
namespace
{
    static const uint64_t primes[4] = {0xA0761D6478BD642FULL, 0xE7037ED1A0B428DBULL, 0x8EBC6AF09C88C6E3ULL, 0x589965CC75374CC3ULL};

    inline uint64_t read2(const uint8_t* p)
    {
        return p[0] | (static_cast<uint64_t>(p[1]) << 8);
    }

    inline uint64_t read4(const uint8_t* p)
    {
        uint32_t x;
        ::memcpy(&x, p, sizeof(uint32_t));
        return x;
    }

    inline uint64_t read8(const uint8_t* p)
    {
        uint64_t x;
        ::memcpy(&x, p, sizeof(uint64_t));
        return x;
    }

    inline uint64_t rot64(uint64_t x)
    {
        return (x >> 32) | (x << 32);
    }

    inline uint64_t rot64(uint64_t x, uint32_t s)
    {
        return (x >> s) | (x << (64 - s));
    }

    void mul(uint64_t& x0, uint64_t& x1)
    {
#if defined(__SIZEOF_INT128__) || (defined(_INTEGRAL_MAX_BITS) && 128 <= _INTEGRAL_MAX_BITS)
        __uint128_t r = x0;
        r *= x1;
        x0 = (uint64_t)r;
        x1 = (uint64_t)(r >> 64U);
#elif defined(_MSC_VER) && defined(_M_X64)
        x0 = _umul128(x0, x1, &x1);
#else
        uint64_t hh = (x0 >> 32) * (x1 >> 32);
        uint64_t hl = (x0 >> 32) * (uint32_t)x1;
        uint64_t lh = (uint32_t)x0 * (x1 >> 32);
        uint64_t ll = (uint64_t)(uint32_t)x0 * (uint32_t)x1;

        x0 = rot64(hl) ^ hh;
        x1 = rot64(lh) ^ ll;
#endif
    }

    inline uint64_t mix(uint64_t x0, uint64_t x1)
    {
        mul(x0, x1);
        return x0 ^ x1;
    }

    uint64_t sphash64(size_t size, const void* data, uint64_t seed = 2685821657736338717ULL)
    {
        const uint8_t* p = reinterpret_cast<const uint8_t*>(data);
        seed ^= primes[0];
        uint64_t x0;
        uint64_t x1;
        if(16 < size) {
            uint64_t s = size;
            if(32 < s) {
                uint64_t seed0 = seed;
                uint64_t seed1 = seed;
                do {
                    seed0 = mix(read8(p) ^ primes[2], read8(p + 8) ^ seed0);
                    seed1 = mix(read8(p + 16) ^ primes[3], read8(p + 24) ^ seed1);
                    p += 32;
                    s -= 32;
                } while(32 < s);
                seed = seed0 ^ seed1;
            }
            while(16 < s) {
                seed = mix(read8(p) ^ primes[1], read8(p + 8) ^ seed);
                p += 16;
                s -= 16;
            }
            x0 = read8(p + s - 16);
            x1 = read8(p + s - 8);
        } else {
            if(4 <= size) {
                switch(size) {
                case 4:
                    x0 = read2(p);
                    x1 = read2(p + 2);
                    break;
                case 5:
                    x0 = read4(p);
                    x1 = p[4];
                    break;
                case 6:
                    x0 = read4(p);
                    x1 = p[4] | (static_cast<uint64_t>(p[5]) << 8);
                    break;
                case 7:
                    x0 = read4(p);
                    x1 = p[4] | (static_cast<uint64_t>(p[5]) << 8) | (static_cast<uint64_t>(p[6]) << 16);
                    break;
                case 8:
                    x0 = read4(p);
                    x1 = p[4] | (static_cast<uint64_t>(p[5]) << 8) | (static_cast<uint64_t>(p[6]) << 16) | (static_cast<uint64_t>(p[7]) << 24);
                    break;
                case 9:
                    x0 = read8(p);
                    x1 = p[8];
                    break;
                case 10:
                    x0 = read8(p);
                    x1 = p[8] | (static_cast<uint64_t>(p[9]) << 8);
                    break;
                case 11:
                    x0 = read8(p);
                    x1 = p[8] | (static_cast<uint64_t>(p[9]) << 8) | (static_cast<uint64_t>(p[10]) << 16);
                    break;
                case 12:
                    x0 = read8(p);
                    x1 = read4(p + 8);
                    break;
                case 13:
                    x0 = read8(p);
                    x1 = read4(p + 8) | (static_cast<uint64_t>(p[12]) << 32);
                    break;
                case 14:
                    x0 = read8(p);
                    x1 = read4(p + 8) | (static_cast<uint64_t>(p[12]) << 32) | (static_cast<uint64_t>(p[13]) << 40);
                    break;
                case 15:
                    x0 = read8(p);
                    x1 = read4(p + 8) | (static_cast<uint64_t>(p[12]) << 32) | (static_cast<uint64_t>(p[13]) << 40) | (static_cast<uint64_t>(p[14]) << 48);
                    break;
                case 16:
                    x0 = read8(p);
                    x1 = read8(p + 8);
                    break;
                default:
                    x0 = x1 = 0;
                    break;
                }
            } else {
                switch(size) {
                case 1:
                    x0 = p[0];
                    break;
                case 2:
                    x0 = p[0] | (static_cast<uint64_t>(p[1]) << 8);
                    break;
                case 3:
                    x0 = p[0] | (static_cast<uint64_t>(p[1]) << 8) | (static_cast<uint64_t>(p[2]) << 16);
                    break;
                default:
                    x0 = 0;
                    break;
                }
                x1 = 0;
            }
        }
        return mix(primes[1] ^ size, mix(x0 ^ primes[1], x1 ^ seed));
    }

    uint64_t get_hash(uint64_t length, const char8_t* str)
    {
        return sphash64(length, str);
    }

    void* open_file(const char8_t* filepath)
    {
        assert(nullptr != filepath);
        void* file = nullptr;
#ifdef _MSC_VER
        file = CreateFileA((const char*)filepath, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
#else
        file = (FILE*)fopen((const char*)filepath, "rb");
#endif
        return file;
    }

    void close_file(void* file)
    {
        if(nullptr != file) {
#ifdef _MSC_VER
            CloseHandle(file);
#else
            fclose((FILE*)file);
#endif
        }
    }

    uint64_t get_size(void* file)
    {
        assert(nullptr != file);
#ifdef _MSC_VER
        LARGE_INTEGER size;
        if(FALSE == GetFileSizeEx(file, &size)) {
            return 0;
        }
        return static_cast<uint64_t>(size.QuadPart);
#else
        struct stat st;
        if(0 != fstat(fileno((FILE*)file), &st)) {
            return 0;
        }
        return st.st_size;
#endif
    }
} // namespace

std::tuple<uint64_t, uint8_t*> read(const char8_t* filepath)
{
    assert(nullptr != filepath);
    void* file = open_file(filepath);
    if(nullptr == file) {
        return {0, nullptr};
    }
    uint64_t size = get_size(file);
    uint8_t* data = new uint8_t[size];
    if(nullptr != data) {
#ifdef _MSC_VER
        uint8_t* tmp = data;
        uint64_t s = size;
        while(0 < s) {
            DWORD blockSize = (size <= 0xFFFF'FFFFULL) ? (DWORD)s : 0xFFFF'FFFFULL;
            DWORD readSize;
            if(TRUE != ReadFile((HANDLE)file, tmp, blockSize, &readSize, nullptr)) {
                break;
            }
            if(0 == readSize) {
                break;
            }
            s -= readSize;
            tmp += readSize;
        }
        bool result = s <= 0;
#else
        bool result = 1 == fread(dst, size, 1, (FILE*)file);
#endif
        if(!result) {
            delete[] data;
            data = nullptr;
            size = 0;
        }
    }
    close_file(file);
    return {size, data};
}

//------------------------------------------------------------
bool operator==(const gguf_metadata_kv_t& x0, const gguf_metadata_kv_t& x1)
{
    if(x0.hash_ != x1.hash_) {
        return false;
    }
    if(x0.key_.length_ != x1.key_.length_ || x0.key_.offset_ != x1.key_.offset_) {
        return false;
    }
    if(x0.value_type_ != x1.value_type_) {
        return false;
    }
    switch(x0.value_type_) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        return x0.value_.uint8_ == x1.value_.uint8_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        return x0.value_.int8_ == x1.value_.int8_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        return x0.value_.uint16_ == x1.value_.uint16_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        return x0.value_.int16_ == x1.value_.int16_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        return x0.value_.uint32_ == x1.value_.uint32_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        return x0.value_.int32_ == x1.value_.int32_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        return x0.value_.uint64_ == x1.value_.uint64_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        return x0.value_.int64_ == x1.value_.int64_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        return x0.value_.float32_ == x1.value_.float32_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        return x0.value_.float64_ == x1.value_.float64_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        return x0.value_.bool_ == x1.value_.bool_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
        return x0.value_.string_.length_ == x1.value_.string_.length_
               && x0.value_.string_.offset_ == x1.value_.string_.offset_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY:
        return x0.value_.array_.type_ == x1.value_.array_.type_
               && x0.value_.array_.size_ == x1.value_.array_.size_
               && x0.value_.array_.offset_ == x1.value_.array_.offset_;
    }
    return false;
}

uint64_t align_offset(uint64_t offset, uint32_t alignment)
{
    return offset + (alignment - (offset % alignment)) % alignment;
}

//--- GGUF
//------------------------------------------------------------
GGUF::GGUF()
    : size_(0)
    , data_(nullptr)
    , tensor_data_(nullptr)
    , alignment_(0)
{
}

GGUF::~GGUF()
{
    size_ = 0;
    delete[] data_;
    data_ = nullptr;
    alignment_ = 0;
}

namespace
{
    uint64_t get_size(const gguf_string_t& str)
    {
        return sizeof(uint64_t) + str.length_;
    }

    uint64_t get_size(const gguf_tensor_info_t& info)
    {
        return get_size(info.name_) + sizeof(uint32_t) + sizeof(uint64_t) * info.n_dimensions_ + sizeof(ggml_type) + sizeof(uint64_t);
    }

    bool valid_type(ggml_type type)
    {
        switch(type) {
        case ggml_type::GGML_TYPE_F32:
            return true;
        case ggml_type::GGML_TYPE_F16:
            return true;
        case ggml_type::GGML_TYPE_Q4_0:
            return true;
        case ggml_type::GGML_TYPE_Q4_1:
            return true;
        case ggml_type::GGML_TYPE_Q5_0:
            return true;
        case ggml_type::GGML_TYPE_Q5_1:
            return true;
        case ggml_type::GGML_TYPE_Q8_0:
            return true;
        case ggml_type::GGML_TYPE_Q8_1:
            return true;
        case ggml_type::GGML_TYPE_Q2_K:
            return true;
        case ggml_type::GGML_TYPE_Q3_K:
            return true;
        case ggml_type::GGML_TYPE_Q4_K:
            return true;
        case ggml_type::GGML_TYPE_Q5_K:
            return true;
        case ggml_type::GGML_TYPE_Q6_K:
            return true;
        case ggml_type::GGML_TYPE_Q8_K:
            return true;
        case ggml_type::GGML_TYPE_IQ2_XXS:
            return true;
        case ggml_type::GGML_TYPE_IQ2_XS:
            return true;
        case ggml_type::GGML_TYPE_IQ3_XXS:
            return true;
        case ggml_type::GGML_TYPE_IQ1_S:
            return true;
        case ggml_type::GGML_TYPE_IQ4_NL:
            return true;
        case ggml_type::GGML_TYPE_IQ3_S:
            return true;
        case ggml_type::GGML_TYPE_IQ2_S:
            return true;
        case ggml_type::GGML_TYPE_IQ4_XS:
            return true;
        case ggml_type::GGML_TYPE_I8:
            return true;
        case ggml_type::GGML_TYPE_I16:
            return true;
        case ggml_type::GGML_TYPE_I32:
            return true;
        case ggml_type::GGML_TYPE_I64:
            return true;
        case ggml_type::GGML_TYPE_F64:
            return true;
        case ggml_type::GGML_TYPE_IQ1_M:
            return true;
        default:
            assert(false);
            return 0;
        }
    }

    uint64_t get_bits(ggml_type type)
    {
        switch(type) {
        case ggml_type::GGML_TYPE_F32:
            return 32;
        case ggml_type::GGML_TYPE_F16:
            return 16;
        case ggml_type::GGML_TYPE_Q4_0:
            return 4;
        case ggml_type::GGML_TYPE_Q4_1:
            return 4;
        case ggml_type::GGML_TYPE_Q5_0:
            return 5;
        case ggml_type::GGML_TYPE_Q5_1:
            return 5;
        case ggml_type::GGML_TYPE_Q8_0:
            return 8;
        case ggml_type::GGML_TYPE_Q8_1:
            return 8;
        case ggml_type::GGML_TYPE_Q2_K:
            return 2;
        case ggml_type::GGML_TYPE_Q3_K:
            return 3;
        case ggml_type::GGML_TYPE_Q4_K:
            return 4;
        case ggml_type::GGML_TYPE_Q5_K:
            return 5;
        case ggml_type::GGML_TYPE_Q6_K:
            return 6;
        case ggml_type::GGML_TYPE_Q8_K:
            return 8;
        case ggml_type::GGML_TYPE_IQ2_XXS:
            return 2;
        case ggml_type::GGML_TYPE_IQ2_XS:
            return 2;
        case ggml_type::GGML_TYPE_IQ3_XXS:
            return 3;
        case ggml_type::GGML_TYPE_IQ1_S:
            return 1;
        case ggml_type::GGML_TYPE_IQ4_NL:
            return 4;
        case ggml_type::GGML_TYPE_IQ3_S:
            return 3;
        case ggml_type::GGML_TYPE_IQ2_S:
            return 2;
        case ggml_type::GGML_TYPE_IQ4_XS:
            return 4;
        case ggml_type::GGML_TYPE_I8:
            return 8;
        case ggml_type::GGML_TYPE_I16:
            return 16;
        case ggml_type::GGML_TYPE_I32:
            return 32;
        case ggml_type::GGML_TYPE_I64:
            return 64;
        case ggml_type::GGML_TYPE_F64:
            return 64;
        case ggml_type::GGML_TYPE_IQ1_M:
            return 1;
        default:
            assert(false);
            return 0;
        }
    }

    uint64_t get_tensor_bits(const gguf_tensor_info_t& info, const uint8_t* data)
    {
        uint64_t size = 0;
        const uint64_t* dimensions = reinterpret_cast<const uint64_t*>(data + info.dimensions_);
        for(uint64_t i = 0; i < info.n_dimensions_; ++i) {
            size += dimensions[i];
        }
        return size * get_bits(info.type_);
    }

    uint64_t get_tensor_size(const gguf_tensor_info_t& info, const uint8_t* data)
    {
        uint64_t size = get_tensor_bits(info, data);
        size = (size + 7ULL) & (~7ULL);
        return size;
    }

    uint32_t get_size(gguf_metadata_value_type type)
    {
        switch(type) {
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
            return 1;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
            return 1;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
            return 2;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
            return 2;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
            return 4;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
            return 4;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
            return 4;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
            return 1;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
            return 8;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY:
            return sizeof(gguf_metadata_value_type) + sizeof(uint64_t);
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
            return 8;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
            return 8;
        case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
            return 8;
        default:
            return 0;
        }
    }
} // namespace

Error GGUF::load(const char8_t* filepath)
{
    assert(nullptr != filepath);
    auto [size, data] = read(filepath);
    if(nullptr == data) {
        return Error::IOError;
    }
    static constexpr uint32_t HeaderSize = 4 + 4 + 8 + 8; // least header size
    if(size <= HeaderSize) {
        delete[] data;
        return Error::InvalidFormat;
    }

    delete[] data_;
    size_ = size;
    data_ = data;
    const gguf_header_t* header = get_header(get_file());
    if(header->magic != 0x46554747UL) {
        return Error::InvalidFormat;
    }

    metadata_.resize(header->metadata_kv_count);
    uintptr_t offset = HeaderSize;
    for(uint64_t i = 0; i < header->metadata_kv_count; ++i) {
        if(Error::Success != parse_metadata(metadata_[i], offset)) {
            return Error::InvalidFormat;
        }
        debug_print(metadata_[i]);
        uint64_t metadata_size = get_metadata_size(metadata_[i]);
        if(metadata_size <= 0 || size_ < (offset + metadata_size)) {
            return Error::InvalidFormat;
        }
        offset += metadata_size;
    }

    tensor_info_.resize(header->tensor_count);
    for(uint64_t i = 0; i < header->tensor_count; ++i) {
        if(Error::Success != parse_tensor_info(tensor_info_[i], offset)) {
            return Error::InvalidFormat;
        }
#ifdef _DEBUG
        std::cout << "[" << i << "] ";
#endif
        debug_print(tensor_info_[i]);
        uint64_t tensor_info_size = get_size(tensor_info_[i]);
        if(size_ < (offset + tensor_info_size)) {
            return Error::InvalidFormat;
        }
        if(!valid_type(tensor_info_[i].type_)) {
            return Error::InvalidFormat;
        }
        offset += tensor_info_size;
    }

    if(!validate_metadate(u8"general.architecture", gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING)) {
        return Error::InvalidFormat;
    }
    if(!validate_metadate(u8"general.quantization_version", gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32)) {
        get_metadata_uint32(quantization_version_, u8"general.quantization_version");
    } else {
        quantization_version_ = 0;
    }
    if(validate_metadate(u8"general.alignment", gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32)) {
        get_metadata_uint32(alignment_, u8"general.alignment");
    } else {
        alignment_ = 4;
    }
    offset = align_offset(offset, alignment_);

    tensor_data_ = data_ + offset;

    uint64_t total_tensor_size = 0;
    for(uint64_t i = 0; i < tensor_info_.size(); ++i) {
        uint64_t tensor_size = get_tensor_size(tensor_info_[i], data_);
        uint64_t end = tensor_info_[i].offset_ + tensor_size;
        if(size_ < end) {
            return Error::InvalidFormat;
        }
        total_tensor_size += tensor_size;
    }
    if(size_ < (offset + total_tensor_size)) {
        return Error::InvalidFormat;
    }
    return Error::Success;
}

const gguf_file_t* GGUF::get_file() const
{
    assert(nullptr != data_);
    return reinterpret_cast<const gguf_file_t*>(data_);
}

const gguf_header_t* GGUF::get_header(const gguf_file_t* file) const
{
    assert(nullptr != file);
    return &file->header;
}

uint64_t GGUF::getNumMetaData() const
{
    return metadata_.size();
}

const gguf_metadata_kv_t& GGUF::getMetaData(uint64_t x) const
{
    return metadata_[x];
}

bool GGUF::getMetaData(const gguf_metadata_kv_t*& metadata, const char8_t* key) const
{
    assert(nullptr != key);
    uint64_t len = ::strlen((const char*)key);
    uint64_t hash = get_hash(len, key);
    for(uint64_t i = 0; i < metadata_.size(); ++i) {
        if(hash == metadata_[i].hash_ && len == metadata_[i].key_.length_) {
            const char* str = reinterpret_cast<const char*>(&data_[metadata_[i].key_.offset_]);
            if(0 == ::strncmp(str, reinterpret_cast<const char*>(key), len)) {
                metadata = &metadata_[i];
                return true;
            }
        }
    }
    metadata = nullptr;
    return false;
}

bool GGUF::getMetaData(const gguf_metadata_kv_t*& metadata, gguf_metadata_value_type type, const char8_t* key) const
{
    assert(nullptr != key);
    uint64_t len = ::strlen((const char*)key);
    uint64_t hash = get_hash(len, key);
    for(uint64_t i = 0; i < metadata_.size(); ++i) {
        if(hash == metadata_[i].hash_
           && len == metadata_[i].key_.length_
           && type == metadata_[i].value_type_) {
            const char* str = reinterpret_cast<const char*>(&data_[metadata_[i].key_.offset_]);
            if(0 == ::strncmp(str, reinterpret_cast<const char*>(key), len)) {
                metadata = &metadata_[i];
                return true;
            }
        }
    }
    metadata = nullptr;
    return false;
}

bool GGUF::getArrayMetaData(const gguf_metadata_kv_t*& metadata, gguf_metadata_value_type type, const char8_t* key) const
{
    assert(nullptr != key);
    uint64_t len = ::strlen((const char*)key);
    uint64_t hash = get_hash(len, key);
    for(uint64_t i = 0; i < metadata_.size(); ++i) {
        const gguf_metadata_kv_t& tmp = metadata_[i];
        if(hash == tmp.hash_
           && len == tmp.key_.length_
           && gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY == tmp.value_type_
           && type == tmp.value_.array_.type_) {
            const char* str = reinterpret_cast<const char*>(&data_[tmp.key_.offset_]);
            if(0 == ::strncmp(str, reinterpret_cast<const char*>(key), len)) {
                metadata = &metadata_[i];
                return true;
            }
        }
    }
    metadata = nullptr;
    return false;
}

uint8_t GGUF::getMetaDataU8(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8 == metadata.value_type_);
    return metadata.value_.uint8_;
}

int8_t GGUF::getMetaDataS8(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8 == metadata.value_type_);
    return metadata.value_.int8_;
}

uint16_t GGUF::getMetaDataU16(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16 == metadata.value_type_);
    return metadata.value_.uint16_;
}

int16_t GGUF::getMetaDataS16(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16 == metadata.value_type_);
    return metadata.value_.int16_;
}

uint32_t GGUF::getMetaDataU32(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32 == metadata.value_type_);
    return metadata.value_.uint32_;
}

int32_t GGUF::getMetaDataS32(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32 == metadata.value_type_);
    return metadata.value_.int32_;
}

uint64_t GGUF::getMetaDataU64(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64 == metadata.value_type_);
    return metadata.value_.uint64_;
}

int64_t GGUF::getMetaDataS64(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64 == metadata.value_type_);
    return metadata.value_.int64_;
}

float GGUF::getMetaDataF32(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32 == metadata.value_type_);
    return metadata.value_.float32_;
}

double GGUF::getMetaDataF64(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64 == metadata.value_type_);
    return metadata.value_.float64_;
}

bool GGUF::getMetaDataBool(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL == metadata.value_type_);
    return metadata.value_.bool_ != 0;
}

GGUFString GGUF::getMetaDataString(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING == metadata.value_type_);
    GGUFString str;
    str.length_ = metadata.value_.string_.length_;
    str.str_ = reinterpret_cast<const char8_t*>(&data_[metadata.value_.string_.offset_]);
    return str;
}

GGUFArray GGUF::getMetaDataArray(const gguf_metadata_kv_t& metadata) const
{
    assert(validate(metadata));
    assert(gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY == metadata.value_type_);
    GGUFArray array;
    array.size_ = metadata.value_.array_.size_;
    array.type_ = metadata.value_.array_.type_;
    array.items_ = &data_[metadata.value_.array_.offset_];
    return array;
}

uint64_t GGUF::getNumTensors() const
{
    return tensor_info_.size();
}

const gguf_tensor_info_t& GGUF::getTensor(uint64_t x) const
{
    return tensor_info_[x];
}

const void* GGUF::getTensorData(uint64_t x) const
{
    const gguf_tensor_info_t& info = tensor_info_[x];
    return &data_[info.offset_];
}

Error GGUF::parse_string(gguf_string_t& str, uintptr_t offset)
{
    if(size_ < (offset + sizeof(uint64_t))) {
        return Error::InvalidFormat;
    }
    ::memcpy(&str.length_, data_ + offset, sizeof(uint64_t));
    offset += sizeof(uint64_t);
    str.offset_ = offset;
    return Error::Success;
}

Error GGUF::parse_metadata(gguf_metadata_kv_t& metadata, uintptr_t offset)
{
    if(Error::Success != parse_string(metadata.key_, offset)) {
        return Error::InvalidFormat;
    }
    offset += get_size(metadata.key_);
    if(size_ < (offset + sizeof(gguf_metadata_value_type))) {
        return Error::InvalidFormat;
    }
    ::memcpy(&metadata.value_type_, data_ + offset, sizeof(gguf_metadata_value_type));
    offset += sizeof(gguf_metadata_value_type);
    if(Error::Success != parse_value(metadata.value_type_, metadata.value_, offset)) {
        return Error::InvalidFormat;
    }
    metadata.hash_ = get_hash(metadata.key_.length_, (const char8_t*)&data_[metadata.key_.offset_]);
    return Error::Success;
}

Error GGUF::parse_value(gguf_metadata_value_type type, gguf_metadata_value_t& value, uintptr_t offset)
{
    uint32_t size = get_size(type);
    if(size_ < (offset + size)) {
        return Error::InvalidFormat;
    }
    switch(type) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        [[fallthrough]];
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        ::memcpy(&value, data_ + offset, size);
        offset += size;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
        ::memcpy(&value, data_ + offset, size);
        offset += size;
        value.string_.offset_ = offset;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY:
        ::memcpy(&value.array_.type_, data_ + offset, sizeof(gguf_metadata_value_type));
        offset += sizeof(gguf_metadata_value_type);
        ::memcpy(&value.array_.size_, data_ + offset, sizeof(uint64_t));
        offset += sizeof(uint64_t);
        value.array_.offset_ = offset;
        break;
    default:
        return Error::InvalidFormat;
    }
    return Error::Success;
}

Error GGUF::parse_tensor_info(gguf_tensor_info_t& info, uintptr_t offset)
{
    // name
    if(Error::Success != parse_string(info.name_, offset)) {
        return Error::InvalidFormat;
    }
    offset += get_size(info.name_);

    // dimensions
    if(size_ < (offset + sizeof(uint32_t))) {
        return Error::InvalidFormat;
    }
    ::memcpy(&info.n_dimensions_, data_ + offset, sizeof(uint32_t));
    offset += sizeof(uint32_t);
    info.dimensions_ = offset;
    {
        uint64_t size = sizeof(uint64_t) * info.n_dimensions_;
        if(size_ < (offset + size)) {
            return Error::InvalidFormat;
        }
        offset += size;
    }

    // type
    if(size_ < (offset + sizeof(ggml_type))) {
        return Error::InvalidFormat;
    }
    ::memcpy(&info.type_, data_ + offset, sizeof(ggml_type));
    offset += sizeof(ggml_type);

    // offset
    if(size_ < (offset + sizeof(uint64_t))) {
        return Error::InvalidFormat;
    }
    ::memcpy(&info.offset_, data_ + offset, sizeof(uint64_t));
    return Error::Success;
}

namespace
{
    bool strequal(uint64_t l0, const char8_t* s0, uint64_t l1, const char8_t* s1)
    {
        if(l0 != l1) {
            return false;
        }
        uint64_t l = (std::min)(l0, l1);
        for(uint64_t i = 0; i < l; ++i) {
            if(s0[i] != s1[i]) {
                return false;
            }
        }
        return true;
    }
} // namespace

bool GGUF::validate_metadate(const char8_t* key, gguf_metadata_value_type type) const
{
    assert(nullptr != key);
    const gguf_metadata_kv_t* metadata = get_metadata(key);
    if(nullptr == metadata) {
        return false;
    }
    return type == metadata->value_type_;
}

const gguf_metadata_kv_t* GGUF::get_metadata(const char8_t* key) const
{
    assert(nullptr != key);
    uint64_t length = ::strlen((const char*)key);
    uint64_t hash = get_hash(length, key);
    for(uint64_t i = 0; i < metadata_.size(); ++i) {
        if(metadata_[i].hash_ != hash) {
            continue;
        }
        const gguf_string_t& k = metadata_[i].key_;
        if(!strequal(length, key, k.length_, (const char8_t*)&data_[k.offset_])) {
            continue;
        }
        return &metadata_[i];
    }
    return nullptr;
}

bool GGUF::get_metadata_uint32(uint32_t& dst, const char8_t* key) const
{
    assert(nullptr != key);
    const gguf_metadata_kv_t* metadata = get_metadata(key);
    if(nullptr != metadata || gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32 != metadata->value_type_) {
        return false;
    }
    dst = metadata->value_.uint32_;
    return true;
}

uint64_t GGUF::get_metadata_size(const gguf_metadata_kv_t& metadata) const
{
    uint64_t key_size = get_size(metadata.key_) + sizeof(gguf_metadata_value_type);
    switch(metadata.value_type_) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        return key_size + 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        return key_size + 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        return key_size + 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        return key_size + 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        return key_size + 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        return key_size + 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        return key_size + 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        return key_size + 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
        return key_size + 8 + metadata.value_.string_.length_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY:
        return key_size + get_array_size(metadata.value_.array_);
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        return key_size + 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        return key_size + 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        return key_size + 8;
    default:
        assert(false);
        return 0;
    }
}

uint64_t GGUF::get_array_size(const gguf_array_t& array) const
{
    uint64_t size = sizeof(gguf_metadata_value_type) + sizeof(uint64_t);
    switch(array.type_) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        return size + array.size_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        return size + array.size_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        return size + array.size_ * 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        return size + array.size_ * 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        return size + array.size_ * 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        return size + array.size_ * 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        return size + array.size_ * 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        return size + array.size_;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING: {
        uint64_t offset = array.offset_;
        for(uint32_t i = 0; i < array.size_; ++i) {
            uint64_t str_size = get_string_size(offset);
            offset += str_size;
            size += str_size;
        }
        return size;
    }
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY: {
        uint64_t offset = array.offset_;
        for(uint32_t i = 0; i < array.size_; ++i) {
            uint64_t value_size = get_value_size(offset, array.type_);
            if(value_size <= 0) {
                return 0;
            }
            offset += value_size;
            size += value_size;
        }
        return size;
    }
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        return size + array.size_ * 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        return size + array.size_ * 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        return size + array.size_ * 8;
    default:
        assert(false);
        return 0;
    }
}

uint64_t GGUF::get_string_size(uint64_t offset) const
{
    if(size_ < (offset + sizeof(uint64_t))) {
        return 0;
    }
    uint64_t length;
    ::memcpy(&length, data_ + offset, sizeof(uint64_t));
    return sizeof(uint64_t) + length;
}

uint64_t GGUF::get_value_size(uint64_t offset, gguf_metadata_value_type type) const
{
    switch(type) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        return 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        return 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        return 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        return 2;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        return 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        return 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        return 4;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        return 1;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
        return get_string_size(offset);
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY: {
        if(size_ < (offset + sizeof(gguf_metadata_value_type))) {
            return 0;
        }

        gguf_metadata_value_type array_type;
        ::memcpy(&array_type, data_ + offset, sizeof(gguf_metadata_value_type));
        offset += sizeof(gguf_metadata_value_type);
        if(size_ < (offset + sizeof(uint64_t))) {
            return 0;
        }
        uint64_t size;
        ::memcpy(&size, data_ + offset, sizeof(uint64_t));
        offset += sizeof(uint64_t);
        uint64_t total_size = 0;
        for(uint32_t i = 0; i < size; ++i) {
            uint64_t value_size = get_value_size(offset, array_type);
            if(value_size <= 0) {
                return 0;
            }
            total_size += value_size;
            offset += value_size;
        }
        return total_size;
    }
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        return 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        return 8;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        return 8;
    default:
        assert(false);
        return 0;
    }
}

bool GGUF::validate(const gguf_metadata_kv_t& metadata) const
{
    for(uint64_t i = 0; i < metadata_.size(); ++i) {
        if(metadata_[i] == metadata) {
            return true;
        }
    }
    return false;
}

void GGUF::debug_print(const gguf_string_t& str) const
{
#ifdef _DEBUG
    const char* s = (const char*)data_ + str.offset_;
    for(uint64_t i = 0; i < str.length_; ++i) {
        std::cout << s[i];
    }
#endif
}

void GGUF::debug_print(gguf_metadata_value_type type, const gguf_metadata_value_t& x) const
{
#ifdef _DEBUG
    switch(type) {
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT8:
        std::cout << x.uint8_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT8:
        std::cout << x.int8_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT16:
        std::cout << x.uint16_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT16:
        std::cout << x.int16_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT32:
        std::cout << x.uint32_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT32:
        std::cout << x.int32_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT32:
        std::cout << x.float32_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_BOOL:
        std::cout << (x.bool_) ? "true" : "false";
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_STRING:
        debug_print(x.string_);
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_ARRAY:
        debug_print(x.array_);
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_UINT64:
        std::cout << x.uint64_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_INT64:
        std::cout << x.uint64_;
        break;
    case gguf_metadata_value_type::GGUF_METADATA_VALUE_TYPE_FLOAT64:
        std::cout << x.float64_;
        break;
    default:
        break;
    }
#endif
}

void GGUF::debug_print(const gguf_array_t& array) const
{
#ifdef _DEBUG
    std::cout << "type:" << (uint32_t)array.type_ << " size:" << array.size_;
#endif
}

void GGUF::debug_print(const gguf_metadata_kv_t& metadata) const
{
#ifdef _DEBUG
    debug_print(metadata.key_);
    std::cout << " : ";
    debug_print(metadata.value_type_, metadata.value_);
    std::cout << std::endl;
#endif
}
void GGUF::debug_print(const gguf_tensor_info_t& info) const
{
#ifdef _DEBUG
    debug_print(info.name_);
    std::cout << ":\n";
    std::cout << "  dimensions [" << info.n_dimensions_ << "]:";
    const uint64_t* dimensions = (const uint64_t*)(&data_[info.dimensions_]);
    for(uint32_t i = 0; i < info.n_dimensions_; ++i) {
        std::cout << dimensions[i] << ',';
    }
    std::cout << '\n';
    std::cout << "  type: " << (uint32_t)info.type_ << std::endl;
#endif
}

} // namespace gguf
