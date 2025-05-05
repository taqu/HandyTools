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
CPLM_EXPORT void* CPLM_STDCALL create_model(uint64_t size, void* memory, int32_t context);
CPLM_EXPORT void CPLM_STDCALL destroy_model(void* model);

CPLM_EXPORT int32_t CPLM_STDCALL generate_one(
	void* model,
	int32_t size,
	char* generated,
	const char* text,
	int32_t context,
	uint64_t seed,
	float temperature,
	float minp,
	int32_t steps);
#ifdef __cplusplus
}
#endif

#endif //CPLM_DLLMAIN_H_
