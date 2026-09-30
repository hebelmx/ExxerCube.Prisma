uv venv --python 3.12
source .venv/bin/activate

uv pip install wheel/sglang-0.0.0.dev11416+g92e8bb79e-py3-none-any.whl
uv pip install --find-links /path/to/wheels/ "sglang[all]"
uv pip install "sglang[all]" --find-links https://flashinfer.ai/whl/cu121/torch2.4/flashinfer/

uv pip install kernels==0.11.7
uv pip install pymupdf==1.27.2.2

uv pip install easydict kernels==0.11.7 pymupdf==1.27.2.2
uv pip install vllm

uv pip install --upgrade sgl_kernel

export SGLANG_ALLOW_OVERWRITE_LONGER_CONTEXT_LEN=1


python -m sglang.launch_server \
    --model baidu/Unlimited-OCR \
    --served-model-name Unlimited-OCR \
    --attention-backend fa3 \
    --page-size 1 \
    --mem-fraction-static 0.8 \
    --context-length 32768 \
    --enable-custom-logit-processor \
    --disable-overlap-schedule \
    --skip-server-warmup \
    --host 0.0.0.0 \
    --port 10000 \
    --trust-remote-code 