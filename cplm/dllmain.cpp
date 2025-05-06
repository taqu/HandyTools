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

    int32_t CPLM_STDCALL generate_one(
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
        params.context_ = context;
        params.seed_ = seed;
        params.temperature_ = temperature;
        params.minp_ = minp;
        params.steps_ = steps;
        params.sequences_ = 1;
        cplm::Result result = model->generate_one((const char8_t*)text, params);
        if(result.text_.size() <= 0) {
            return 0;
        }
        std::size_t len = (std::min)(result.text_.size() + 1, (std::size_t)size);
        strcpy_s(generated, len, (const char*)result.text_.c_str());
        return (int32_t)(len - 1);
    }
#ifdef __cplusplus
}
#endif
