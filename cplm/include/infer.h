#ifndef INC_INFER_H_
#define INC_INFER_H_
#include <cstdint>

struct Transformer;

namespace cplm
{
	float* forward(::Transformer* transformer, int32_t token, int32_t pos, uint32_t flags);
}
#endif //INC_INFER_H_

