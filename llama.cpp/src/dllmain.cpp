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

    void* CPLM_STDCALL begin(void* memory0, int32_t size, const uint16_t* prompt, int32_t n_predict, float temperature, uint32_t seed)
    {
        assert(nullptr != memory0);
        llama::Model* model = (llama::Model*)memory0;

        size_t size_utf8 = utf16_to_utf8(prompt, size, nullptr, 0);
        utf8_t* text_utf8 = (utf8_t*)::malloc(sizeof(utf8_t)*(size_utf8+1));
        size_utf8 = utf16_to_utf8(prompt, size, text_utf8, size_utf8);
        text_utf8[size_utf8] = u8'\0';

        llama::Context* context = model->begin((const char8_t*)text_utf8, n_predict, temperature, seed);
        ::free(text_utf8);
        return context;
    }

    void CPLM_STDCALL end(void* memory0, void* memory1)
    {
        assert(nullptr != memory0);
        assert(nullptr != memory1);
        llama::Model* model = (llama::Model*)memory0;
        llama::Context* context = (llama::Context*)memory1;
        model->end(context);
    }

    int32_t CPLM_STDCALL generate(void* memory0, void* memory1, int32_t size, uint16_t* output)
    {
        assert(nullptr != memory0);
        assert(nullptr != memory1);
        assert(0<size);
        llama::Model* model = (llama::Model*)memory0;
        llama::Context* context = (llama::Context*)memory1;

        int32_t s = size * 4 + 1;
        char8_t* tmp = (char8_t*)::malloc(sizeof(char8_t)*s);
        int32_t r = model->generate(size, tmp, context);
        if(r<0){
            ::free(tmp);
            return -1;
        }
        convert(size, output, tmp);
        return r;
    }

    int32_t CPLM_STDCALL get_fim_prefix(void* memory, int32_t size, uint16_t* str)
    {
        assert(nullptr != memory);
        assert(0<size);
        llama::Model* model = (llama::Model*)memory;
        const struct llama_vocab* vocab = model->vocab();
        llama_token token = llama_vocab_fim_pre(vocab);
        if(token<0){
            return -1;
        }
        const char* text = llama_vocab_get_text(vocab, token);
        if(nullptr == text){
            return -1;
        }
        convert(size, str, (const char8_t*)text);
        return token;
    }

    int32_t CPLM_STDCALL get_fim_middle(void* memory, int32_t size, uint16_t* str)
    {
        assert(nullptr != memory);
        assert(0<size);
        llama::Model* model = (llama::Model*)memory;
        const struct llama_vocab* vocab = model->vocab();
        llama_token token = llama_vocab_fim_mid(vocab);
        if(token<0){
            return -1;
        }
        const char* text = llama_vocab_get_text(vocab, token);
        if(nullptr == text){
            return -1;
        }
        convert(size, str, (const char8_t*)text);
        return token;
    }

    int32_t CPLM_STDCALL get_fim_suffix(void* memory, int32_t size, uint16_t* str)
    {
        assert(nullptr != memory);
        assert(0<size);
        llama::Model* model = (llama::Model*)memory;
        const struct llama_vocab* vocab = model->vocab();
        llama_token token = llama_vocab_fim_suf(vocab);
        if(token<0){
            return -1;
        }
        const char* text = llama_vocab_get_text(vocab, token);
        if(nullptr == text){
            return -1;
        }
        convert(size, str, (const char8_t*)text);
        return token;
    }

    int32_t CPLM_STDCALL get_fim_pad(void* memory, int32_t size, uint16_t* str)
    {
        assert(nullptr != memory);
        assert(0<size);
        llama::Model* model = (llama::Model*)memory;
        const struct llama_vocab* vocab = model->vocab();
        llama_token token = llama_vocab_fim_pad(vocab);
        if(token<0){
            return -1;
        }
        const char* text = llama_vocab_get_text(vocab, token);
        if(nullptr == text){
            return -1;
        }
        convert(size, str, (const char8_t*)text);
        return token;
    }
#ifdef __cplusplus
}
#endif
