import base64
import json
import os
import tempfile

import fitz
import requests
from sglang.srt.sampling.custom_logit_processor import DeepseekOCRNoRepeatNGramLogitProcessor

server_url = "http://127.0.0.1:10000"

session = requests.Session()
session.trust_env = False


def pdf_to_images(pdf_path, dpi=300):
    doc = fitz.open(pdf_path)
    tmp_dir = tempfile.mkdtemp(prefix="pdf_ocr_")
    mat = fitz.Matrix(dpi / 72, dpi / 72)
    image_paths = []
    for i, page in enumerate(doc):
        image_path = os.path.join(tmp_dir, f"page_{i + 1:04d}.png")
        page.get_pixmap(matrix=mat).save(image_path)
        image_paths.append(image_path)
    doc.close()
    return image_paths


def encode_image(image_path):
    ext = os.path.splitext(image_path)[1].lower()
    mime = "image/jpeg" if ext in (".jpg", ".jpeg") else f"image/{ext.lstrip('.')}"
    with open(image_path, "rb") as f:
        data = base64.b64encode(f.read()).decode("utf-8")
    return {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{data}"}}


def build_content(prompt, image_paths):
    return [{"type": "text", "text": prompt}] + [encode_image(path) for path in image_paths]


def generate(prompt, image_paths, image_mode, ngram_window):
    payload = {
        "model": "Unlimited-OCR",
        "messages": [{"role": "user", "content": build_content(prompt, image_paths)}],
        "temperature": 0,
        "skip_special_tokens": False,
        "images_config": {"image_mode": image_mode},
        "custom_logit_processor": DeepseekOCRNoRepeatNGramLogitProcessor.to_str(),
        "custom_params": {
            "ngram_size": 35,
            "window_size": ngram_window,
        },
        "stream": True,
    }
    response = session.post(
        f"{server_url}/v1/chat/completions",
        headers={"Content-Type": "application/json"},
        data=json.dumps(payload),
        timeout=1200,
        stream=True,
    )
    response.raise_for_status()

    chunks = []
    for line in response.iter_lines(chunk_size=1, decode_unicode=True):
        if not line or not line.startswith("data: "):
            continue
        data = line[len("data: "):]
        if data == "[DONE]":
            break
        event = json.loads(data)
        delta = event["choices"][0].get("delta", {}).get("content", "")
        if delta:
            print(delta, end="", flush=True)
            chunks.append(delta)
    print()
    return "".join(chunks)


# Single image supports two configs: gundam or base. Example below uses gundam.
generate("document parsing.", ["your_image.jpg"], image_mode="gundam", ngram_window=128)

# Multi image (base only)
generate("Multi page parsing.", ["page1.png", "page2.png"], image_mode="base", ngram_window=1024)

# PDF (base only)
generate("Multi page parsing.", pdf_to_images("your_doc.pdf", dpi=300), image_mode="base", ngram_window=1024)