#ifndef CPLM_DLLMAIN_H_
#define CPLM_DLLMAIN_H_
#include <cstdint>
#include <Windows.h>

#ifdef cplm_EXPORTS
#define CPLM_EXPORT __declspec(dllexport)
#else
#define CPLM_EXPORT __declspec(dllimport)
#endif
#define CPLM_STDCALL __stdcall

#ifdef __cplusplus
extern "C" {
#endif
CPLM_EXPORT void* CPLM_STDCALL create_model(const uint16_t* path, int32_t n_gpu_layers);
CPLM_EXPORT void CPLM_STDCALL destroy_model(void* model);
CPLM_EXPORT int32_t CPLM_STDCALL get_fim_prefix(void* model, int32_t size, uint16_t* str);
CPLM_EXPORT int32_t CPLM_STDCALL get_fim_middle(void* model, int32_t size, uint16_t* str);
CPLM_EXPORT int32_t CPLM_STDCALL get_fim_suffix(void* model, int32_t size, uint16_t* str);
CPLM_EXPORT int32_t CPLM_STDCALL get_fim_pad(void* model, int32_t size, uint16_t* str);

CPLM_EXPORT void CPLM_STDCALL generate_one(
	void* model,
	uint32_t buffer_size,
	uint16_t* generated,
	uint32_t size,
	const uint16_t* text,
	int32_t context,
	uint32_t seed,
	float temperature,
	float minp,
	int32_t steps,
	int32_t stop_token);
#ifdef __cplusplus
}
#endif

#endif //CPLM_DLLMAIN_H_
