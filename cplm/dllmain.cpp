#include "dllmain.h"
#include "cplm.h"

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

#ifdef __cplusplus
extern "C"
{
#endif
    void* CPLM_STDCALL create_model(uint64_t size, void* memory, int32_t context)
    {
        assert(nullptr != memory);
        cplm::Model* model = new cplm::Model();
        if(nullptr == model) {
            return nullptr;
        }
        if(!model->open(size, memory, context)) {
            CPLM_DELETE(model);
            return nullptr;
        }
        return model;
    }

    void CPLM_STDCALL destroy_model(void* memory)
    {
        if(nullptr == memory) {
            return;
        }
        cplm::Model* model = (cplm::Model*)memory;
        CPLM_DELETE(model);
    }

    void CPLM_STDCALL generate_one(
        void* memory,
        int32_t size,
        char* generated,
        const char* text,
        int32_t context,
        uint64_t seed,
        float temperature,
        float minp,
        int32_t steps)
    {
        assert(nullptr != memory);
        assert(0 < size);
        assert(nullptr != generated);
        assert(nullptr != text);
        cplm::Model* model = (cplm::Model*)memory;
        cplm::Model::Params params;
        CPLM_LOG_PRINT("prompt: %s\n", text);
        params.context_ = context;
        params.seed_ = seed;
        params.temperature_ = temperature;
        params.minp_ = minp;
        params.steps_ = steps;
        params.sequences_ = 1;
        cplm::Result result = model->generate_one((const char8_t*)text, params);
        if(result.text_.size() <= 0) {
            return;
        }
        std::size_t len = (std::min)(result.text_.size(), (std::size_t)(size-1));
        for(std::size_t i=0; i<len; ++i){
            generated[i] = (char)result.text_[i];
        }
        generated[len] = '\0';
    }

    bool CPLM_STDCALL is_gpu(void* memory)
    {
        assert(nullptr != memory);
        const cplm::Model* model = (const cplm::Model*)memory;
        return model->is_cuda();
    }

    int32_t CPLM_STDCALL get_fim_prefix(void* memory, int32_t size, uint8_t* str)
    {
        static const char8_t* fim_prefix0 = u8"<|fim_prefix|>";
        static const char8_t* fim_prefix1 = u8"<|fim-prefix|>";
        static const char8_t* fim_prefix2 = u8"<fim_prefix>";
        static const char8_t* fim_prefix3 = u8"<fim-prefix>";
        assert((int32_t)strlen((const char*)fim_prefix0)<size);

        assert(nullptr != memory);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_prefix;
        fim_prefix = tokenizer.find(fim_prefix0);
        if(0<=fim_prefix){
            ::memcpy(str, fim_prefix0, strlen((const char*)fim_prefix0)+1);
            return (int32_t)strlen((const char*)fim_prefix0);
        }
        fim_prefix = tokenizer.find(fim_prefix1);
        if(0<=fim_prefix){
            ::memcpy(str, fim_prefix1, strlen((const char*)fim_prefix1)+1);
            return (int32_t)strlen((const char*)fim_prefix1);
        }
        fim_prefix = tokenizer.find(fim_prefix2);
        if(0<=fim_prefix){
            ::memcpy(str, fim_prefix2, strlen((const char*)fim_prefix2)+1);
            return (int32_t)strlen((const char*)fim_prefix2);
        }
        fim_prefix = tokenizer.find(fim_prefix3);
        if(0<=fim_prefix){
            ::memcpy(str, fim_prefix3, strlen((const char*)fim_prefix3)+1);
            return (int32_t)strlen((const char*)fim_prefix3);
        }
        return 0;
    }

    int32_t CPLM_STDCALL get_fim_middle(void* memory, int32_t size, uint8_t* str)
    {
        static const char8_t* fim_middle0 = u8"<|fim_middle|>";
        static const char8_t* fim_middle1 = u8"<|fim-middle|>";
        static const char8_t* fim_middle2 = u8"<fim_middle>";
        static const char8_t* fim_middle3 = u8"<fim-middle>";
        assert((int32_t)strlen((const char*)fim_middle0)<size);

        assert(nullptr != memory);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_middle;
        fim_middle = tokenizer.find(fim_middle0);
        if(0<=fim_middle){
            ::memcpy(str, fim_middle0, strlen((const char*)fim_middle0)+1);
            return (int32_t)strlen((const char*)fim_middle0);
        }
        fim_middle = tokenizer.find(fim_middle1);
        if(0<=fim_middle){
            ::memcpy(str, fim_middle1, strlen((const char*)fim_middle1)+1);
            return (int32_t)strlen((const char*)fim_middle1);
        }
        fim_middle = tokenizer.find(fim_middle2);
        if(0<=fim_middle){
            ::memcpy(str, fim_middle2, strlen((const char*)fim_middle2)+1);
            return (int32_t)strlen((const char*)fim_middle2);
        }
        fim_middle = tokenizer.find(fim_middle3);
        if(0<=fim_middle){
            ::memcpy(str, fim_middle3, strlen((const char*)fim_middle3)+1);
            return (int32_t)strlen((const char*)fim_middle3);
        }
        return 0;
    }

    int32_t CPLM_STDCALL get_fim_suffix(void* memory, int32_t size, uint8_t* str)
    {
        static const char8_t* fim_suffix0 = u8"<|fim_suffix|>";
        static const char8_t* fim_suffix1 = u8"<|fim-suffix|>";
        static const char8_t* fim_suffix2 = u8"<fim_suffix>";
        static const char8_t* fim_suffix3 = u8"<fim-suffix>";
        assert((int32_t)strlen((const char*)fim_suffix0)<size);

        assert(nullptr != memory);
        const cplm::Model* model = (const cplm::Model*)memory;
        const cplm::Tokenizer& tokenizer = model->get_tokenizer();
        int32_t fim_suffix;
        fim_suffix = tokenizer.find(fim_suffix0);
        if(0<=fim_suffix){
            ::memcpy(str, fim_suffix0, strlen((const char*)fim_suffix0)+1);
            return (int32_t)strlen((const char*)fim_suffix0);
        }
        fim_suffix = tokenizer.find(fim_suffix1);
        if(0<=fim_suffix){
            ::memcpy(str, fim_suffix1, strlen((const char*)fim_suffix1)+1);
            return (int32_t)strlen((const char*)fim_suffix1);
        }
        fim_suffix = tokenizer.find(fim_suffix2);
        if(0<=fim_suffix){
            ::memcpy(str, fim_suffix2, strlen((const char*)fim_suffix2)+1);
            return (int32_t)strlen((const char*)fim_suffix2);
        }
        fim_suffix = tokenizer.find(fim_suffix3);
        if(0<=fim_suffix){
            ::memcpy(str, fim_suffix3, strlen((const char*)fim_suffix3)+1);
            return (int32_t)strlen((const char*)fim_suffix3);
        }
        return 0;
    }


#ifdef __cplusplus
}
#endif
