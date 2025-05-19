#include "dllmain.h"
#include <cassert>
#include "model.h"
#include "converter.h"

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD ul_reason_for_call,
                      LPVOID lpReserved)
{
    switch(ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

namespace
{
    size_t strlen_u16(const uint16_t* path)
{
        size_t len = 0;
        while(path[len] != 0){
            ++len;
        }
        return len;
}
}

#ifdef __cplusplus
extern "C"
{
#endif
    static void convert(size_t size, uint16_t* dst, const char8_t* src)
    {
        assert(0<size);
        size_t size_utf16 = utf8_to_utf16((const utf8_t*)src, strlen((const char*)src), dst, size-1);
        assert(size_utf16<size);
        dst[size_utf16] = u'\0';
    }

    void* CPLM_STDCALL create_model(const uint16_t* path, int32_t n_gpu_layers)
    {
        assert(nullptr != path);
        assert(0<=n_gpu_layers);
        size_t size = strlen_u16(path);
        size_t size_utf8 = utf16_to_utf8(path, size, nullptr, 0);
        utf8_t* text_utf8 = (utf8_t*)::malloc(sizeof(utf8_t)*(size_utf8+1));
        size_utf8 = utf16_to_utf8(path, size, text_utf8, size_utf8);
        text_utf8[size_utf8] = u8'\0';

        llama::Model* model = llama::Model::load((const char8_t*)text_utf8, n_gpu_layers);
        if(nullptr == model) {
            return nullptr;
        }
        return model;
    }

    void CPLM_STDCALL destroy_model(void* memory)
    {
        if(nullptr == memory) {
            return;
        }
        llama::Model* model = (llama::Model*)memory;
        delete model;
    }

    void CPLM_STDCALL generate_one(
        void* memory,
        uint32_t buffer_size,
        uint16_t* generated,
        uint32_t size,
        const uint16_t* text,
        int32_t context,
        uint32_t seed,
        float temperature,
        float minp,
        int32_t steps,
        int32_t stop_token)
    {
        assert(nullptr != memory);
        assert(0 < buffer_size);
        assert(nullptr != generated);
        assert(nullptr != text);
        size_t size_utf8 = utf16_to_utf8(text, size, nullptr, 0);
        utf8_t* text_utf8 = (utf8_t*)::malloc(sizeof(utf8_t)*(size_utf8+1));
        size_utf8 = utf16_to_utf8(text, size, text_utf8, size_utf8);
        text_utf8[size_utf8] = u8'\0';

        llama::Model* model = (llama::Model*)memory;
        llama::Context* context = model->begin((const char8_t*)text_utf8, context, temperature, seed);
        params.context_ = context;
        params.seed_ = seed;
        params.temperature_ = temperature;
        params.minp_ = minp;
        params.steps_ = steps;
        params.sequences_ = 1;
        params.stop0_ = stop_token;
        cplm::Result result = model->generate_one((const char8_t*)text_utf8, params);
        if(result.text_.size() <= 0) {
            CPLM_FREE(text_utf8);
            return;
        }
        size_t size_utf16 = utf8_to_utf16((const utf8_t*)result.text_.c_str(), result.text_.size(), nullptr, 0);
        std::size_t len = (std::min)(size_utf16, (std::size_t)(buffer_size-1));
        size_utf16 = utf8_to_utf16((const utf8_t*)result.text_.c_str(), result.text_.size(), generated, len);
        assert(size_utf16<buffer_size);
        generated[size_utf16] = u'\0';
        CPLM_FREE(text_utf8);
    }

    int32_t CPLM_STDCALL get_fim_prefix(void* memory, int32_t size, uint16_t* str)
    {
        static const char8_t* fim_prefix0 = u8"<|fim_prefix|>";
        static const char8_t* fim_prefix1 = u8"<|fim-prefix|>";
        static const char8_t* fim_prefix2 = u8"<fim_prefix>";
        static const char8_t* fim_prefix3 = u8"<fim-prefix>";

        assert(nullptr != memory);
        assert(0<size);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_prefix;
        fim_prefix = tokenizer.find(fim_prefix0);
        if(0<=fim_prefix){
            convert(size, str, fim_prefix0);
            return fim_prefix;
        }
        fim_prefix = tokenizer.find(fim_prefix1);
        if(0<=fim_prefix){
            convert(size, str, fim_prefix1);
            return fim_prefix;
        }
        fim_prefix = tokenizer.find(fim_prefix2);
        if(0<=fim_prefix){
            convert(size, str, fim_prefix2);
            return fim_prefix;
        }
        fim_prefix = tokenizer.find(fim_prefix3);
        if(0<=fim_prefix){
            convert(size, str, fim_prefix3);
            return fim_prefix;
        }
        return -1;
    }

    int32_t CPLM_STDCALL get_fim_middle(void* memory, int32_t size, uint16_t* str)
    {
        static const char8_t* fim_middle0 = u8"<|fim_middle|>";
        static const char8_t* fim_middle1 = u8"<|fim-middle|>";
        static const char8_t* fim_middle2 = u8"<fim_middle>";
        static const char8_t* fim_middle3 = u8"<fim-middle>";

        assert(nullptr != memory);
        assert(0<size);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_middle;
        fim_middle = tokenizer.find(fim_middle0);
        if(0<=fim_middle){
            convert(size, str, fim_middle0);
            return fim_middle;
        }
        fim_middle = tokenizer.find(fim_middle1);
        if(0<=fim_middle){
            convert(size, str, fim_middle1);
            return fim_middle;
        }
        fim_middle = tokenizer.find(fim_middle2);
        if(0<=fim_middle){
            convert(size, str, fim_middle2);
            return fim_middle;
        }
        fim_middle = tokenizer.find(fim_middle3);
        if(0<=fim_middle){
            convert(size, str, fim_middle3);
            return fim_middle;
        }
        return -1;
    }

    int32_t CPLM_STDCALL get_fim_suffix(void* memory, int32_t size, uint16_t* str)
    {
        static const char8_t* fim_suffix0 = u8"<|fim_suffix|>";
        static const char8_t* fim_suffix1 = u8"<|fim-suffix|>";
        static const char8_t* fim_suffix2 = u8"<fim_suffix>";
        static const char8_t* fim_suffix3 = u8"<fim-suffix>";

        assert(nullptr != memory);
        assert(0<size);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_suffix;
        fim_suffix = tokenizer.find(fim_suffix0);
        if(0<=fim_suffix){
            convert(size, str, fim_suffix0);
            return fim_suffix;
        }
        fim_suffix = tokenizer.find(fim_suffix1);
        if(0<=fim_suffix){
            convert(size, str, fim_suffix1);
            return fim_suffix;
        }
        fim_suffix = tokenizer.find(fim_suffix2);
        if(0<=fim_suffix){
            convert(size, str, fim_suffix2);
            return fim_suffix;
        }
        fim_suffix = tokenizer.find(fim_suffix3);
        if(0<=fim_suffix){
            convert(size, str, fim_suffix3);
            return fim_suffix;
        }
        return -1;
    }

    int32_t CPLM_STDCALL get_fim_pad(void* memory, int32_t size, uint16_t* str)
    {
        static const char8_t* fim_pad0 = u8"<|fim_pad|>";
        static const char8_t* fim_pad1 = u8"<|fim-pad|>";
        static const char8_t* fim_pad2 = u8"<fim_pad>";
        static const char8_t* fim_pad3 = u8"<fim-pad>";

        assert(nullptr != memory);
        assert(0<size);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_pad;
        fim_pad = tokenizer.find(fim_pad0);
        if(0<=fim_pad){
            convert(size, str, fim_pad0);
            return fim_pad;
        }
        fim_pad = tokenizer.find(fim_pad1);
        if(0<=fim_pad){
            convert(size, str, fim_pad1);
            return fim_pad;
        }
        fim_pad = tokenizer.find(fim_pad2);
        if(0<=fim_pad){
            convert(size, str, fim_pad2);
            return fim_pad;
        }
        fim_pad = tokenizer.find(fim_pad3);
        if(0<=fim_pad){
            convert(size, str, fim_pad3);
            return fim_pad;
        }
        return -1;
    }
#ifdef __cplusplus
}
#endif
