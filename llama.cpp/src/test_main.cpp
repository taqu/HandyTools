#include "llama.h"
#include <cstdio>
#include <string>
#include <vector>
#include "model.h"

int main(void)
{
    llama::Model* model = llama::Model::load(u8"qwen2.5-coder-0.5b-instruct-q4_k_m.gguf", 99);
    if(nullptr == model){
        return 1;
    }
    const auto t_main_start = ggml_time_us();
    llama::Context* context = model->begin(u8"クイックソートをPythonで書いてください", 1024);
    if(nullptr == context){
        delete model;
        return 1;
    }
    char8_t output[1024];
    int32_t n_decode = model->generate(1024, output, context);
    model->end(context);
    fprintf(stderr, "%s\n", output);
    const auto t_main_end = ggml_time_us();
    fprintf(stderr, "\n%s: decoded %d tokens in %.2f s, speed: %.2f t/s\n",
            __func__, n_decode, (t_main_end - t_main_start) / 1000000.0f, n_decode / ((t_main_end - t_main_start) / 1000000.0f));
    const struct llama_vocab* vocab = model->vocab();
    llama_token token = llama_vocab_fim_suf(vocab);
    const char* suffix = llama_vocab_get_text(vocab, token);
    delete model;
	return 0;
}
