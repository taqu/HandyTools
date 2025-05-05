#ifndef INC_GGUF_H_
#define INC_GGUF_H_
/**
 * GGUF format from https://github.com/ggerganov/ggml/blob/master/docs/gguf.md
 */
#include <cassert>
#include <cstdint>
#include <cstring>
#include <type_traits>
#include <utility>
#include <tuple>

namespace gguf
{
enum class ggml_type : uint32_t
{
    GGML_TYPE_F32 = 0,
    GGML_TYPE_F16 = 1,
    GGML_TYPE_Q4_0 = 2,
    GGML_TYPE_Q4_1 = 3,
    // GGML_TYPE_Q4_2 = 4, support has been removed
    // GGML_TYPE_Q4_3 = 5, support has been removed
    GGML_TYPE_Q5_0 = 6,
    GGML_TYPE_Q5_1 = 7,
    GGML_TYPE_Q8_0 = 8,
    GGML_TYPE_Q8_1 = 9,
    GGML_TYPE_Q2_K = 10,
    GGML_TYPE_Q3_K = 11,
    GGML_TYPE_Q4_K = 12,
    GGML_TYPE_Q5_K = 13,
    GGML_TYPE_Q6_K = 14,
    GGML_TYPE_Q8_K = 15,
    GGML_TYPE_IQ2_XXS = 16,
    GGML_TYPE_IQ2_XS = 17,
    GGML_TYPE_IQ3_XXS = 18,
    GGML_TYPE_IQ1_S = 19,
    GGML_TYPE_IQ4_NL = 20,
    GGML_TYPE_IQ3_S = 21,
    GGML_TYPE_IQ2_S = 22,
    GGML_TYPE_IQ4_XS = 23,
    GGML_TYPE_I8 = 24,
    GGML_TYPE_I16 = 25,
    GGML_TYPE_I32 = 26,
    GGML_TYPE_I64 = 27,
    GGML_TYPE_F64 = 28,
    GGML_TYPE_IQ1_M = 29,
    GGML_TYPE_COUNT,
};

enum class gguf_metadata_value_type : uint32_t
{
    // The value is a 8-bit unsigned integer.
    GGUF_METADATA_VALUE_TYPE_UINT8 = 0,
    // The value is a 8-bit signed integer.
    GGUF_METADATA_VALUE_TYPE_INT8 = 1,
    // The value is a 16-bit unsigned little-endian integer.
    GGUF_METADATA_VALUE_TYPE_UINT16 = 2,
    // The value is a 16-bit signed little-endian integer.
    GGUF_METADATA_VALUE_TYPE_INT16 = 3,
    // The value is a 32-bit unsigned little-endian integer.
    GGUF_METADATA_VALUE_TYPE_UINT32 = 4,
    // The value is a 32-bit signed little-endian integer.
    GGUF_METADATA_VALUE_TYPE_INT32 = 5,
    // The value is a 32-bit IEEE754 floating point number.
    GGUF_METADATA_VALUE_TYPE_FLOAT32 = 6,
    // The value is a boolean.
    // 1-byte value where 0 is false and 1 is true.
    // Anything else is invalid, and should be treated as either the model being invalid or the reader being buggy.
    GGUF_METADATA_VALUE_TYPE_BOOL = 7,
    // The value is a UTF-8 non-null-terminated string, with length prepended.
    GGUF_METADATA_VALUE_TYPE_STRING = 8,
    // The value is an array of other values, with the length and type prepended.
    ///
    // Arrays can be nested, and the length of the array is the number of elements in the array, not the number of bytes.
    GGUF_METADATA_VALUE_TYPE_ARRAY = 9,
    // The value is a 64-bit unsigned little-endian integer.
    GGUF_METADATA_VALUE_TYPE_UINT64 = 10,
    // The value is a 64-bit signed little-endian integer.
    GGUF_METADATA_VALUE_TYPE_INT64 = 11,
    // The value is a 64-bit IEEE754 floating point number.
    GGUF_METADATA_VALUE_TYPE_FLOAT64 = 12,
};

// A string in GGUF.
struct gguf_string_t
{
    uint64_t length_;
    uint64_t offset_;
};

// An array in GGUF.
struct gguf_array_t
{
    // Any value type is valid, including arrays.
    gguf_metadata_value_type type_;
    // Number of elements, not bytes
    uint64_t size_;
    // The array of values.
    uint64_t offset_;
};

union gguf_metadata_value_t
{
    uint8_t uint8_;
    int8_t int8_;
    uint16_t uint16_;
    int16_t int16_;
    uint32_t uint32_;
    int32_t int32_;
    float float32_;
    uint64_t uint64_;
    int64_t int64_;
    double float64_;
    uint8_t bool_;
    gguf_string_t string_;
    gguf_array_t array_;
};

struct gguf_metadata_kv_t
{
    uint64_t hash_;
    // The key of the metadata. It is a standard GGUF string, with the following caveats:
    // - It must be a valid ASCII string.
    // - It must be a hierarchical key, where each segment is `lower_snake_case` and separated by a `.`.
    // - It must be at most 2^16-1/65535 bytes long.
    // Any keys that do not follow these rules are invalid.
    gguf_string_t key_;

    // The type of the value.
    // Must be one of the `gguf_metadata_value_type` values.
    gguf_metadata_value_type value_type_;
    // The value.
    gguf_metadata_value_t value_;
};

bool operator==(const gguf_metadata_kv_t& x0, const gguf_metadata_kv_t& x1);

struct gguf_header_t
{
    // Magic number to announce that this is a GGUF file.
    // Must be `GGUF` at the byte level: `0x47` `0x47` `0x55` `0x46`.
    // Your executor might do little-endian byte order, so it might be
    // check for 0x46554747 and letting the endianness cancel out.
    // Consider being *very* explicit about the byte order here.
    uint32_t magic;
    // The version of the format implemented.
    // Must be `3` for version described in this spec, which introduces big-endian support.
    //
    // This version should only be increased for structural changes to the format.
    // Changes that do not affect the structure of the file should instead update the metadata
    // to signify the change.
    uint32_t version;
    // The number of tensors in the file.
    // This is explicit, instead of being included in the metadata, to ensure it is always present
    // for loading the tensors.
    uint64_t tensor_count;
    // The number of metadata key-value pairs.
    uint64_t metadata_kv_count;
    // The metadata key-value pairs.
    //gguf_metadata_kv_t metadata_kv[metadata_kv_count];
};

uint64_t align_offset(uint64_t offset, uint32_t alignment = 32UL);

struct gguf_tensor_info_t
{
    // The name of the tensor. It is a standard GGUF string, with the caveat that
    // it must be at most 64 bytes long.
    gguf_string_t name_;
    // The number of dimensions in the tensor.
    // Currently at most 4, but this may change in the future.
    uint32_t n_dimensions_;
    // The dimensions of the tensor.
    uint64_t dimensions_;//dimensions[n_dimensions];
    // The type of the tensor.
    ggml_type type_;
    // The offset of the tensor's data in this file in bytes.
    //
    // This offset is relative to `tensor_data`, not to the start
    // of the file, to make it easier for writers to write the file.
    // Readers should consider exposing this offset relative to the
    // file to make it easier to read the data.
    //
    // Must be a multiple of `ALIGNMENT`. That is, `align_offset(offset) == offset`.
    uint64_t offset_;
};

struct gguf_file_t
{
    // The header of the file.
    gguf_header_t header;

    // Tensor infos, which can be used to locate the tensor data.
    gguf_tensor_info_t* tensor_infos; // tensor_infos[header.tensor_count];

    // Padding to the nearest multiple of `ALIGNMENT`.
    //
    // That is, if `sizeof(header) + sizeof(tensor_infos)` is not a multiple of `ALIGNMENT`,
    // this padding is added to make it so.
    //
    // This can be calculated as `align_offset(position) - position`, where `position` is
    // the position of the end of `tensor_infos` (i.e. `sizeof(header) + sizeof(tensor_infos)`).
    //uint8_t _padding[];

    // Tensor data.
    //
    // This is arbitrary binary data corresponding to the weights of the model. This data should be close
    // or identical to the data in the original model file, but may be different due to quantization or
    // other optimizations for inference. Any such deviations should be recorded in the metadata or as
    // part of the architecture definition.
    //
    // Each tensor's data must be stored within this array, and located through its `tensor_infos` entry.
    // The offset of each tensor's data must be a multiple of `ALIGNMENT`, and the space between tensors
    // should be padded to `ALIGNMENT` bytes.
    //uint8_t tensor_data[];
};

//--- Utilities
//------------------------------------------------------------
std::tuple<uint64_t, uint8_t*> read(const char8_t* filepath);

enum class Error
{
    Success = 0,
    Unknown,
    InvalidFormat,
    IOError,
};

//--- Array
//------------------------------------------------------------
template<class T>
class Array
{
    static_assert(std::is_trivially_copyable<T>::value == true, "T should be trivially copyable.");
public:
    inline static constexpr uint64_t Expand = 16;
    Array();
    ~Array();
    uint64_t capacity() const;
    uint64_t size() const;
    const T& operator[](uint64_t index) const;
    T& operator[](uint64_t index);
    void clear();
    bool reserve(uint64_t capacity);
    bool resize(uint64_t capacity);
    bool push_back(const T& x);

private:
    Array(const Array&) = delete;
    Array& operator=(const Array&) = delete;
    bool expand(uint64_t capacity);
    uint64_t capacity_;
    uint64_t size_;
    T* items_;
};

template<class T>
Array<T>::Array()
    : capacity_(0)
    , size_(0)
    , items_(nullptr)
{
}

template<class T>
Array<T>::~Array()
{
    delete[] items_;
    items_ = nullptr;
}

template<class T>
uint64_t Array<T>::capacity() const
{
    return capacity_;
}

template<class T>
uint64_t Array<T>::size() const
{
    return size_;
}

template<class T>
const T& Array<T>::operator[](uint64_t index) const
{
    assert(index < size_);
    return items_[index];
}

template<class T>
T& Array<T>::operator[](uint64_t index)
{
    assert(index < size_);
    return items_[index];
}

template<class T>
void Array<T>::clear()
{
    size_ = 0;
}

template<class T>
bool Array<T>::reserve(uint64_t capacity)
{
    capacity = (std::max)(size_, capacity);
    uint64_t new_capacity = Expand;
    while(new_capacity<capacity){
        new_capacity += Expand;
    }
    return expand(new_capacity);
}

template<class T>
bool Array<T>::resize(uint64_t size)
{
    size = (std::max)(capacity_, size);
    uint64_t new_capacity = Expand;
    while(new_capacity<size){
        new_capacity += Expand;
    }
    if(expand(new_capacity)){
        assert(size<new_capacity);
        size_ = size;
        return true;
    }else{
        return false;
    }
}

template<class T>
bool Array<T>::push_back(const T& x)
{
    if(capacity_<=size_){
        if(!expand(capacity_+Expand)){
            return false;
        }
        assert(size_<capacity_);
    }
    items_[size_] = x;
    ++size_;
    return true;
}

template<class T>
bool Array<T>::expand(uint64_t capacity)
{
    if(capacity<=capacity_){
        return true;;
    }
    T* items = new T[capacity];
    if(nullptr != items_) {
        ::memcpy(items, items_, sizeof(T) * capacity);
        delete[] items_;
    }
    items_ = items;
    capacity_ = capacity;
    return true;
}

struct GGUFString
{
    uint64_t length_;
    const char8_t* str_;
};

//--- GGUFArray
//------------------------------------------------------------
struct GGUFArray
{
    template<class T>
    struct Iterator
    {
        Iterator(uint64_t size, const uint8_t* data)
            :size_(size)
            ,count_(0)
            ,data_(data)
        {
        }

        operator bool() const
        {
            return count_<size_;
        }

        Iterator<T>& operator++()
        {
            ++count_;
            data_ += sizeof(T);
            return *this;
        }

        T operator*()
        {
            T t;
            ::memcpy(&t, data_, sizeof(T));
            return t;
        }

        uint64_t size_;
        uint64_t count_;
        const uint8_t* data_;
    };

    template<>
    struct Iterator<GGUFString>
    {
        Iterator(uint64_t size, const uint8_t* data)
            :size_(size)
            ,count_(0)
            ,data_(data)
        {
        }

        operator bool() const
        {
            return count_<size_;
        }

        Iterator<GGUFString>& operator++()
        {
            ++count_;
            uint64_t length;
            ::memcpy(&length, data_, sizeof(uint64_t));
            data_ += sizeof(uint64_t);
            data_ += length;
            return *this;
        }

        GGUFString operator*()
        {
            GGUFString str;
            ::memcpy(&str.length_, data_, sizeof(uint64_t));
            str.str_ = reinterpret_cast<const char8_t*>(data_ + sizeof(uint64_t));
            return str;
        }

        uint64_t size_;
        uint64_t count_;
        const uint8_t* data_;
    };


    template<class T>
    Iterator<T> begin() const
    {
        return Iterator<T>(size_, items_);
    }

    template<class T>
    T get(uint64_t x) const
    {
        assert(x<size_);
        T r;
        ::memcpy(&r, &items_[x*sizeof(T)], sizeof(T));
        return r;
    }

    gguf_metadata_value_type type_;
    uint64_t size_;
    const uint8_t* items_;
};

//--- GGUF
//------------------------------------------------------------
class GGUF
{
public:
    GGUF();
    ~GGUF();

    Error load(const char8_t* filepath);
    uint64_t getNumMetaData() const;
    const gguf_metadata_kv_t& getMetaData(uint64_t x) const;
    bool getMetaData(const gguf_metadata_kv_t*& metadata, const char8_t* key) const;
    bool getMetaData(const gguf_metadata_kv_t*& metadata, gguf_metadata_value_type type, const char8_t* key) const;
    bool getArrayMetaData(const gguf_metadata_kv_t*& metadata, gguf_metadata_value_type type, const char8_t* key) const;
    
    uint8_t getMetaDataU8(const gguf_metadata_kv_t& metadata) const;
    int8_t getMetaDataS8(const gguf_metadata_kv_t& metadata) const;
    uint16_t getMetaDataU16(const gguf_metadata_kv_t& metadata) const;
    int16_t getMetaDataS16(const gguf_metadata_kv_t& metadata) const;
    uint32_t getMetaDataU32(const gguf_metadata_kv_t& metadata) const;
    int32_t getMetaDataS32(const gguf_metadata_kv_t& metadata) const;
    uint64_t getMetaDataU64(const gguf_metadata_kv_t& metadata) const;
    int64_t getMetaDataS64(const gguf_metadata_kv_t& metadata) const;
    float getMetaDataF32(const gguf_metadata_kv_t& metadata) const;
    double getMetaDataF64(const gguf_metadata_kv_t& metadata) const;
    bool getMetaDataBool(const gguf_metadata_kv_t& metadata) const;
    GGUFString getMetaDataString(const gguf_metadata_kv_t& metadata) const;
    GGUFArray getMetaDataArray(const gguf_metadata_kv_t& metadata) const;

    uint64_t getNumTensors() const;
    const gguf_tensor_info_t& getTensor(uint64_t x) const;
    const void* getTensorData(uint64_t x) const;

private:
    GGUF(const GGUF&) = delete;
    GGUF& operator=(const GGUF&) = delete;
    Error parse_string(gguf_string_t& str, uintptr_t offset);
    Error parse_metadata(gguf_metadata_kv_t& metadata, uintptr_t offset);
    Error parse_value(gguf_metadata_value_type type, gguf_metadata_value_t& value, uintptr_t offset);
    Error parse_tensor_info(gguf_tensor_info_t& info, uintptr_t offset);
    const gguf_file_t* get_file() const;
    const gguf_header_t* get_header(const gguf_file_t* file) const;
    bool validate_metadate(const char8_t* key, gguf_metadata_value_type type) const;
    const gguf_metadata_kv_t* get_metadata(const char8_t* key) const;
    bool get_metadata_uint32(uint32_t& dst, const char8_t* key) const;

    uint64_t get_metadata_size(const gguf_metadata_kv_t& metadata) const;
    uint64_t get_array_size(const gguf_array_t& array) const;
    uint64_t get_string_size(uint64_t offset) const;
    uint64_t get_value_size(uint64_t offset, gguf_metadata_value_type type) const;

    bool validate(const gguf_metadata_kv_t& metadata) const;

    void debug_print(const gguf_string_t& str) const;
    void debug_print(gguf_metadata_value_type type, const gguf_metadata_value_t& x) const;
    void debug_print(const gguf_array_t& array) const;
    void debug_print(const gguf_metadata_kv_t& metadata) const;
    void debug_print(const gguf_tensor_info_t& info) const;

    uint64_t size_;
    const uint8_t* data_;
    const uint8_t* tensor_data_;
    uint32_t quantization_version_;
    uint32_t alignment_;
    Array<gguf_metadata_kv_t> metadata_;
    Array<gguf_tensor_info_t> tensor_info_;
};
} // namespace gguf
#endif // INC_GGUF_H_
