<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# GRELECY — AI and gaming research roadmap

Updated: **2026-10-31**. Status: **proposed experiments; execution has not started**.

This action plan turns the 31 October 2026 AI/gaming feasibility study into bounded, reviewable work. It prioritizes usable inference and game-specific improvements on existing hardware. It does not establish measured GRELECY performance gains or a software release commitment.

Tracking issue: **[Roadmap #5](https://github.com/FaipL/Grelecy/issues/5)**. The issue holds current task status; this document holds the plan and acceptance rules.

## Scope and starting evidence

- Hardware remains **Kepler, Maxwell, Pascal, and Turing-based GeForce GTX 16 only**, as defined in [Project scope](PROJECT_SCOPE.md). RTX 20/30/40 are comparison material, outside this delivery scope.
- Windows is the target platform. Linux results can help explain a mechanism but do not validate its Windows behavior.
- One GTX 1060 is recorded as available to the project initiator. Its VRAM capacity, exact variant and test environment still need confirmation. No GTX 16, Maxwell or Kepler test card is assumed available.
- The existing [Diagnostics preview](DIAGNOSTICS.md) can help prepare voluntary configuration reports. It does not benchmark applications, measure VRAM, infer architecture or establish compatibility.
- No new AI, gaming, unlocker or ReBAR experiment was executed to prepare this roadmap. Upstream measurements remain attributed upstream reports.

The starting preference for Pascal is a resource choice, not a change to community membership or a support promise. All four families can contribute evidence immediately.

## Priorities by hardware

| Family | First useful work | Toolchain hypothesis to verify | Firmware research |
| --- | --- | --- | --- |
| Kepler | Identify exact compute capability; preserve a small useful workload if feasible | CC 3.0 generally needs CUDA 10.x; CC 3.5 needs an appropriate CUDA 11.x stack; engine and libraries may impose stricter limits | No direct path in the studied unlocker |
| Maxwell | Find compatible inference engines and game configurations | Evaluate a pinned CUDA 12.x stack and exact architecture targets; do not assume current engines still build | No direct path in the studied unlocker |
| Pascal | First Windows baseline on the available GTX 1060; quantized inference and per-game profiles | Evaluate CUDA 12.x, including 12.9; confirm engine, compiler, libraries and driver together | No direct path in the studied unlocker |
| GTX 16 Turing | Confirm exact chip/PCI identity; compare ordinary CUDA, MMQ/DP4A and applicable FP16 paths | Validate against the selected engine; GTX 16 must not be treated as RTX Tensor hardware | Separate unlocker and ReBAR feasibility reviews |

CUDA versions above are research starting points, not install recommendations. CUDA 13 removes offline compilation/library support for pre-Turing families including Maxwell and Pascal. Kepler has earlier cutoffs. Existing executables, toolkit targets, library support and driver compatibility are separate questions. Pin the last workable combination for each scenario; a successful compile alone is insufficient.

## Execution sequence

Weeks are an **illustrative 10–12 week research cycle from an agreed kickoff**, assuming contributors and hardware become available. They are not dates or effort estimates. The maintainer coordinates decisions; task ownership remains unassigned until someone volunteers. Tasks may move forward on independent hardware after their prerequisites are met.

| Gate | Indicative window | Priority / lead role needed | Deliverable and exit condition | Dependency / tracking |
| --- | --- | --- | --- | --- |
| M0 — Inventory and measurement contract | Weeks 1–2 | P0 / Windows tester + benchmark reviewer | Exact GTX 1060 identity/VRAM, public hardware availability table, fixed AI inputs and three candidate games, reproducible A/B protocol | [#6](https://github.com/FaipL/Grelecy/issues/6) |
| M1 — Reproducible inference stacks | Weeks 2–4 | P0 / C++ and CUDA build contributor | Pinned Windows build recipes, CPU fallback, actual GPU execution and correctness evidence for the first card; other families explicitly marked untested or blocked | M0; [#7](https://github.com/FaipL/Grelecy/issues/7) |
| M2 — Two useful AI demonstrators | Weeks 4–6 | P0 / inference tester | Quantized small LLM plus local transcription, each compared with CPU and an unchanged GPU baseline; correctness, speed and memory results published | M0 + M1; [#8](https://github.com/FaipL/Grelecy/issues/8) and [#9](https://github.com/FaipL/Grelecy/issues/9) |
| M3 — Per-game improvements | Weeks 4–8 | P1 / graphics and Windows tester | Three repeatable game scenarios; native vs applicable upscalers/settings; measured frame times, image quality, VRAM and rollback | M0; [#10](https://github.com/FaipL/Grelecy/issues/10); can overlap M1/M2 |
| M4 — Reviewed evidence and next objective | Weeks 8–12 | P1 / reviewer + documentation contributor | Scenario-level result catalogue, independent reproduction where possible, explicit continue/stop decisions and one selected follow-up objective | M2 + M3; [#11](https://github.com/FaipL/Grelecy/issues/11) |
| R1 — GTX 16 unlocker feasibility | After M0; time-boxed review | P2 / GPU firmware researcher | Source review and a go/no-go note for one exact GTX 16; any later hardware trial requires a separately documented experiment | Exact test card + reviewer + M0; [#12](https://github.com/FaipL/Grelecy/issues/12); does not block M1–M4 |
| R2 — GTX 16 ReBAR feasibility | After M0; time-boxed review | P2 / motherboard firmware researcher | Platform-specific benefit/risk and recovery assessment, separate from Tensor throughput research | Exact card/board + reviewer + M0; [#13](https://github.com/FaipL/Grelecy/issues/13); does not block M1–M4 |

**First actions:** confirm the GTX 1060 variant and VRAM; recruit a Windows tester and build contributor; select fixed licensed models/audio and three games accessible to testers; publish the M0 protocol before collecting performance claims. If hardware or contributors are missing, record the blocker and continue source/documentation work without inventing results.

## Work packages

### M0 — Hardware and reproducible measurements

Record exact model, desktop/laptop variant, PCI device/subsystem IDs when voluntarily supplied, architecture/compute capability with provenance, VRAM from a reliable tool or specification, Windows edition/build, driver package and exposed API capabilities. Separate detected values, user statements and unknowns. Do not use misleading WMI memory values or infer a GTX 1650's chip solely from its product name.

Choose one small quantized LLM fitting the confirmed VRAM, one fixed transcription corpus with reference text, and three repeatable game scenes: a legacy DX11 case, a DX12 case with built-in FSR/XeSS where available, and a VRAM-constrained case. A game may cover two categories, but three scenarios must be recorded. Final titles depend on tester access; game/model assets are linked rather than redistributed without rights.

Before testing, freeze the scene/input, output resolution, quality preset, quantization, context length, batch/offload settings and the definition of a useful result. Warm up both configurations, alternate A/B order for at least five measured pairs, and repeat on a second session. Keep raw runs, median and spread; increase repetitions when the apparent difference is near measurement noise. Record clocks, temperature, power mode and background load when available; use stock limits for the initial baseline.

### M1 — Build and compatibility matrix

For each tested scenario, pin upstream commit, toolkit, compiler, CMake options, architecture targets, dependencies, Windows and driver. Record build commands, artifact hashes, license obligations, model/source provenance and fallback behavior. Verify actual GPU use; silent CPU fallback is a separate result.

Start with Pascal on the available card. Recruit one GTX 16 test system next; add Maxwell and Kepler rows as hardware and engine compatibility permit. Split Kepler compute capabilities rather than promising one universal build. If a current engine cannot target a family, investigate a pinned older engine or another backend, and document the maintenance/security cost before recommending it. No official GPU support claim follows from a build recipe.

### M2 — AI inference and local transcription

- **LLM:** evaluate `llama.cpp` on a small quantized model, with CPU reference, prompt processing and token generation measured separately. Capture model/hash/license, quantization, context, offload, RAM/VRAM, time to first token if available, output correctness and stability. Inspect Pascal DP4A/MMQ and GTX 16 paths before selecting build options. Assess upstream PR 25479 at a pinned base/head; its reported gain is a hypothesis to reproduce, not a target or a dependency on its merger.
- **Transcription:** evaluate `whisper.cpp` with a fixed audio/reference corpus. Record model/hash/license, backend, wall time, real-time factor, RAM/VRAM and word error rate with the normalization rules stated. Compare the same model and input on CPU and GPU before separately evaluating quantization. Unsupported CUDA/Vulkan paths remain explicit blockers.

Correctness checks must cover a fixed evaluation set, not just plausible-looking output. For a kernel change, compare numerical output or logits with documented tolerances and a quality metric; for transcription, compare the reference transcript. Publish quality changes caused by quantization separately from speed changes.

### M3 — Gaming profiles, upscaling and compatibility

Compare stock/native rendering with reversible settings, texture/VRAM profiles and available NIS/FSR/XeSS modes, at the same output resolution with internal resolution and quality differences recorded. Start with integrations already present in the game. A temporal upscaler needs engine data such as depth and motion vectors; an SDK is not a universal injection solution. Verify exposed DirectX/Shader Model and DP4A capabilities before attempting a particular XeSS path, and check that SDK/version's actual requirements.

Measure average FPS, 1% lows with the calculation defined, median/p95/p99 frame times, stutters, crashes, peak VRAM and image quality on a fixed scene. Capture matching stills/motion samples for ghosting, shimmer, HUD and disocclusion artifacts. Use the same power settings and record CPU limitations. Treat a lower-resolution or lower-quality speedup as a tradeoff, rather than an equivalent-quality compute improvement.

DXVK or other translation layers require exact driver/API/version validation. Windows behavior and anti-cheat rules must be checked for the chosen title; prefer offline tests permitting modifications and provide removal/rollback steps. Do not recommend the latest DXVK indiscriminately across all families.

Frame generation is an optional later experiment after a stable rendered baseline. Report **rendered FPS, displayed FPS, latency and image quality separately**. GTX cards are outside AMD's published NVIDIA target for FSR 3/3.1 frame generation. Other paths, including XeSS FG, need their own version-specific capability and integration checks. No frame-generation support is promised by this plan.

### M4 — Evidence catalogue and adoption decision

Each result row identifies exact GPU/VRAM, OS/driver, application/engine/model or game version, settings, reproduction commands, raw data, correctness/quality, measured delta/spread, limitations, tester pseudonym if offered, review status and rollback. Use **not tested**, **blocked**, **observed once**, **reproduced**, or **rejected** for the specific scenario; do not label an entire GPU universally compatible.

For a performance recommendation, require two sessions with a repeatable improvement larger than observed run-to-run noise, no unexplained correctness/stability regression, and a stated quality/latency tradeoff. A compatibility fix can succeed without a speed gain if it reproducibly restores the named workload. A null or negative result is a useful deliverable and can close a research task.

Start with reviewed Markdown/CSV evidence and manual reports. Evaluate a future Diagnostics or launcher integration only after useful combinations are established. A separate implementation proposal must address report-schema compatibility, all 14 localizations, explicit user review, optional collection and no automatic upload. This roadmap changes no application behavior or telemetry.

## Separate firmware research gates

**R1: `nv-uefi-unlocker`.** At the reviewed upstream revision, GTX 16 is described as theoretically compatible and awaiting hardware validation; pre-Turing has no supported transaction path. Device routing is not proof of usable Tensor hardware or application benefit. GTX 16 has no officially exposed RTX Tensor/DLSS feature set. First identify an operation that the exact chip can execute and that the proposed change could affect. Review Windows GSP/driver prerequisites, initialization, handoff, persistence after driver load, cold-power reset and recovery. Keep firmware provenance and distribution rights explicit. Time-box the source assessment to two contributor-weeks; stop if no plausible executable accelerated path can be established. Do not add Pascal IDs to the tool as a compatibility claim.

Only a later, scoped lab experiment should attempt the full chain: changed state, survival into Windows, correct computation, changed microbenchmark and useful application benefit. A register write or EFI success alone is not success. Do not introduce firmware writes or boot changes into the Diagnostics prototype or routine community instructions.

**R2: ReBAR / `NvStrapsReBar`.** This concerns GPU memory mapping, not the Tensor throughput mechanism. The documented approach modifies and flashes the **motherboard UEFI**, which is materially different from the unlocker's volatile GPU state. Review exact board/UEFI/GPU, Above 4G/CSM/PCI resource constraints, driver and per-game behavior, and a credible recovery path before proposing any flash. Time-box this initial feasibility review to two contributor-weeks. Defer hardware work if a qualified tester or recovery route is unavailable; close as no-go if the platform cannot meet the prerequisites. Any future trial compares identical game settings and can reject the change when it regresses performance. This plan authorizes research planning, not a firmware flash.

## Delivery and review rules

- Every execution ticket needs a volunteer owner, hardware availability, pinned inputs, results or a recorded blocker, and a reviewer before its gate is marked complete.
- Maintainer review selects which experimental recipes become recommendations. Initial figures such as 2× Tensor throughput or upstream Pascal percentage gains must never appear as GRELECY results without reproduction.
- Keep source/license notices; review third-party rights before publishing binaries, models or firmware. Link legitimate upstream downloads and retain checksums/build recipes where redistribution is not established.
- Publish reports after removing account names, machine names, serials, local paths and unrelated data. Reuse Diagnostics' explicit review and voluntary sharing approach.
- Completion of this roadmap means evidence and a chosen next objective. Release engineering, signed distribution, a new driver, universal DLSS or universal GPU support require separate decisions.

## Sources and provenance

The referenced ChatGPT study, *Étude IA jeux vidéo* (31 October 2026), supplied the research questions. Its performance numbers were not treated as GRELECY evidence. The following upstream records were checked when preparing the plan; re-pin sources before executing each experiment.

- [`nv-uefi-unlocker` compatibility matrix at `2d68d2e`](https://github.com/physicsdolphin/nv-uefi-unlocker/blob/2d68d2eb6cc27fd9c8cc437e4a4872acdd179444/docs/compatibility.en.md): GTX 16 validation remains pending; pre-Turing excluded. Follow the matching README/FAQ for revision-specific prerequisites.
- [`llama.cpp` source at `ddf48f85`](https://github.com/ggml-org/llama.cpp/tree/ddf48f85c933c1f438212a02eb21e7b001d2d37d): inspect build targets and CUDA backend before choosing flags.
- [`llama.cpp` PR 25479](https://github.com/ggml-org/llama.cpp/pull/25479), open at review; candidate head `daec666d1416c4a5cda55fcb8fad87a3f10c928d`: upstream author's GTX 1060 6 GB DP4A measurements, requiring independent reproduction.
- [`whisper.cpp`](https://github.com/ggml-org/whisper.cpp): candidate transcription engine; per-GPU compatibility still to establish.
- [`NvStrapsReBar` at `5ce12bb5`](https://github.com/terminatorul/NvStrapsReBar/blob/5ce12bb5585c19cd54eefe670910ebf19793586d/README.md): motherboard UEFI modification and platform prerequisites.
- [NVIDIA CUDA release notes](https://docs.nvidia.com/cuda/cuda-toolkit-release-notes/) and [legacy compute capabilities](https://developer.nvidia.com/cuda-legacy-gpus): verify version-specific targets, libraries and model identity.
- [NVIDIA Image Scaling](https://github.com/NVIDIAGameWorks/NVIDIAImageScaling), [AMD FSR 2](https://github.com/GPUOpen-Effects/FidelityFX-FSR2), [Intel XeSS](https://github.com/intel/xess), and [DXVK](https://github.com/doitsujin/dxvk): capability/integration references, not certifications of GRELECY hardware.

## Synthèse opérationnelle en français

Le périmètre reste **Kepler, Maxwell, Pascal et GTX 16 Turing, Windows en priorité**. Le premier banc visé est la GTX 1060 disponible, dont la variante et la VRAM doivent être confirmées. Aucun gain GRELECY n'est encore mesuré.

1. **M0 :** inventorier le matériel, recruter les responsables et figer les scènes, modèles et règles de comparaison.
2. **M1 :** établir une chaîne d'inférence Windows reproductible par configuration, avec contrôle de l'exécution GPU et repli CPU.
3. **M2 :** comparer un petit LLM quantifié et une transcription locale, en mesurant vitesse, mémoire et qualité.
4. **M3 :** tester trois scénarios de jeux, profils VRAM et upscalers disponibles ; publier FPS, temps d'image et défauts visuels.
5. **M4 :** publier les résultats vérifiables, les échecs et les limites ; choisir un objectif suivant à partir des preuves.
6. **R1/R2 :** instruire séparément le débridage GTX 16 et ReBAR. Une étude UEFI ne constitue ni une promesse de gain, ni une autorisation de flash.

Le cycle de 10 à 12 semaines est indicatif à partir d'un démarrage convenu, conditionné aux volontaires et au matériel. Les tickets font foi pour l'avancement. Les essais UEFI et la génération d'images ne bloquent pas les travaux logiciels utiles.
