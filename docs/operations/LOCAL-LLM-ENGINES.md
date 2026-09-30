# Local LLM engines on this box (Ollama · llama.cpp · Colibri)

> Status 2026-07-21 · applies to the dev box `Exxerpro` (RTX 3060 12 GB, 60 GB RAM).
> Shared by ExxerCube.Prisma and IndFusion.CubeXplorer — a copy of this note lives in both repos.

## TL;DR for agents

- **Ollama is the default engine — keep using it.** Benchmarked head-to-head, Ollama (CUDA)
  already extracts ~all the GPU's throughput: llama3.1:8b Q4_K_M → Ollama **58.1 tok/s** vs
  direct llama.cpp Vulkan **55.6 tok/s** (CPU-only llama.cpp: 12.4 tok/s). Don't switch for speed.
- **Direct llama.cpp exists for *control*, not speed:** parallel request slots / continuous
  batching (`--parallel N` — Ollama serializes per model), pinned models (no keep-alive unload
  between batch runs), fixed context, speculative decoding, clean OpenAI-compatible endpoint.
  Reach for it in batch document-extraction workloads with concurrent requests.

## llama.cpp (built + verified)

- Repo: `~/ExxerProjects/IndFusion/llama.cpp` (sibling checkout). Builds: `build/` (CPU),
  `build-vulkan/` (GPU — RTX 3060 via Vulkan; CUDA was abandoned, corrupted Ubuntu package).
- Launcher: `./serve-local.sh <ollama-model-name>` → OpenAI-compatible API at
  `http://127.0.0.1:8086/v1` (`/v1/chat/completions`, `/v1/models`, SSE streaming).
  It serves the **existing Ollama GGUF blobs directly** (`/usr/share/ollama/.ollama/models/blobs/`)
  — no model re-downloads. Known models: llama3.1:8b, qwen2.5-coder:14b, gemma3:12b,
  qwen3-coder:30b, granite3.2-vision, gemma.
- **Port map:** 8080–8085 and 18081/18091 are taken by the Prisma/Veriqan stacks — llama-server
  defaults to **8086**. (Gotcha: the Veriqan worker on 8080 answers `/health` convincingly.)
- Gotchas: blob paths are content hashes — after `ollama pull` updates a model, refresh the path
  in `serve-local.sh` (`ollama show <m> --modelfile | grep FROM`). `granite3.2-vision` image input
  needs its separate `--mmproj` projector blob (not wired yet — text-only via the script).

## Colibri (built, dormant)

- Repo: `~/ExxerProjects/IndFusion/colibri`. Single-purpose engine: runs **only GLM-5.2 744B MoE**
  by streaming experts from NVMe. Engine compiles and self-tests OK, but the model is a
  **372 GB download** (owner decision pending) and expect low single-digit tok/s (disk-bound).
  Not a substitute for Ollama/llama.cpp; it's optional access to frontier-scale quality locally.
  OpenAI-compatible via `./coli serve` if ever activated.
