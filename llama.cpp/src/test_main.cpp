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
#if 0
    int32_t n_predict = 64;
	for(size_t i=0; i<ggml_backend_dev_count(); ++i){
		ggml_backend_dev_t dev = ggml_backend_dev_get(i);
        //ggml_backend_dev_type type = 
		printf("device [%u]:\n", i);
		printf(" name: %s\n", ggml_backend_dev_name(dev));
		printf(" reg: %s\n", ggml_backend_reg_name(ggml_backend_dev_backend_reg(dev)));
	}

	ggml_backend_t best_dev = ggml_backend_init_best();
	printf(" name: %s\n", ggml_backend_name(best_dev));
	llama_model_params model_params = llama_model_default_params();
    model_params.n_gpu_layers = 99;

    llama_model * model = llama_model_load_from_file("qwen2.5-coder-0.5b-instruct-q4_k_m.gguf", model_params);
    if (!model) {
        fprintf(stderr , "%s: error: unable to load model\n" , __func__);
        return 1;
    }

    auto sparams = llama_sampler_chain_default_params();
    sparams.no_perf = false;
    llama_sampler * smpl = llama_sampler_chain_init(sparams);

    llama_sampler_chain_add(smpl, llama_sampler_init_greedy());

    const llama_vocab * vocab = llama_model_get_vocab(model);

	std::string prompt = "#write a quick sort algorithm";
	// tokenize the prompt

    // find the number of tokens in the prompt
    const int n_prompt = -llama_tokenize(vocab, prompt.c_str(), prompt.size(), NULL, 0, true, true);

    // allocate space for the tokens and tokenize the prompt
    std::vector<llama_token> prompt_tokens(n_prompt);
    if (llama_tokenize(vocab, prompt.c_str(), prompt.size(), prompt_tokens.data(), prompt_tokens.size(), true, true) < 0) {
        fprintf(stderr, "%s: error: failed to tokenize the prompt\n", __func__);
        return 1;
    }

    llama_context_params ctx_params = llama_context_default_params();
    // n_ctx is the context size
    ctx_params.n_ctx = n_prompt + n_predict - 1;
    // n_batch is the maximum number of tokens that can be processed in a single call to llama_decode
    ctx_params.n_batch = n_prompt;
    // enable performance counters
    ctx_params.no_perf = false;

    llama_context * ctx = llama_init_from_model(model, ctx_params);

    if (ctx == NULL) {
        fprintf(stderr , "%s: error: failed to create the llama_context\n" , __func__);
        return 1;
    }

    llama_batch batch = llama_batch_get_one(prompt_tokens.data(), prompt_tokens.size());

    // main loop

    const auto t_main_start = ggml_time_us();
    int n_decode = 0;
    llama_token new_token_id;

    for (int n_pos = 0; n_pos + batch.n_tokens < n_prompt + n_predict; ) {
        // evaluate the current batch with the transformer model
        if (llama_decode(ctx, batch)) {
            fprintf(stderr, "%s : failed to eval, return code %d\n", __func__, 1);
            return 1;
        }

        n_pos += batch.n_tokens;

        // sample the next token
        {
            new_token_id = llama_sampler_sample(smpl, ctx, -1);

            // is it an end of generation?
            if (llama_vocab_is_eog(vocab, new_token_id)) {
                break;
            }

            char buf[128];
            int n = llama_token_to_piece(vocab, new_token_id, buf, sizeof(buf), 0, true);
            if (n < 0) {
                fprintf(stderr, "%s: error: failed to convert token to piece\n", __func__);
                return 1;
            }
            std::string s(buf, n);
            printf("%s", s.c_str());
            fflush(stdout);

            // prepare the next batch with the sampled token
            batch = llama_batch_get_one(&new_token_id, 1);

            n_decode += 1;
        }
    }

    printf("\n");

    llama_sampler_free(smpl);
    llama_free(ctx);
	llama_model_free(model);
    #endif
    const auto t_main_start = ggml_time_us();
    llama::Context* context = model->begin(u8"#write a quick sort algorithm", 256);
    if(nullptr == context){
        delete model;
        return 1;
    }
    int32_t n_decode = model->generate(context);
    model->end(context);
    const auto t_main_end = ggml_time_us();

    fprintf(stderr, "\n%s: decoded %d tokens in %.2f s, speed: %.2f t/s\n",
            __func__, n_decode, (t_main_end - t_main_start) / 1000000.0f, n_decode / ((t_main_end - t_main_start) / 1000000.0f));
    delete model;
	return 0;
}
