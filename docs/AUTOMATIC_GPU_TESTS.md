<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# Automatic GPU tests — protocol 1

This document describes the implementation in the **0.4.0-preview.1 source**. It is not a claim of a published release, a measured improvement, or validation on any particular GTX card. GRELECY's research scope remains Kepler, Maxwell, Pascal and GTX 16 Turing, with Windows first.

## What the first version establishes

| Check | Evidence produced | Limits |
| --- | --- | --- |
| DXGI inventory | Adapter name, dedicated video memory, optional PCI model IDs, user-mode driver version when available | Memory is the amount reported by DXGI, not free memory or measured usable capacity. No architecture is guessed from a name. |
| Direct3D 11 | Device creation on the explicitly selected adapter; negotiated feature level 11_0 or 11_1 | This is not a complete feature-level inventory, DX12/Shader Model/Vulkan check or a game launch. |
| Compute correctness | Compiled `cs_5_0` workload executes and every output element matches the CPU reference after each measured pass | This single integer workload does not test all instruction types, all VRAM, numerical AI quality or long-term stability. |
| Synthetic performance | Five GPU timestamp durations in milliseconds, minimum, median and maximum | Not gaming FPS, model throughput, peak FLOPS, power efficiency or a general GPU score. |
| CUDA driver probe | Initialization, maximum driver API version and compute capability from the CUDA device uniquely matching the DXGI adapter | No CUDA kernel, framework, model or toolkit installation is tested. Old drivers without reliable matching produce an inconclusive result. |

The driver version in this report comes from `IDXGIAdapter::CheckInterfaceSupport(IID_IDXGIDevice)`, when available. It describes the user-mode driver; do not assume it is identical to the WMI driver version or the vendor's commercial package number. A missing version remains unknown.

## Fixed compute scenario

Identifier: **`d3d11-uint-mix-v1`**. Changing the kernel, compile flags, dimensions, warm-up policy or measured interval requires a new scenario identifier.

- 262,144 independent unsigned 32-bit outputs (1 MiB); 256 threads per group and 1,024 groups.
- Three warm-up dispatches, followed by five measured dispatches. A different seed is used for every pass; a stale output cannot validate a new measured pass.
- Initial value: `(index + 1) XOR (seed × 0x9E3779B9)`, modulo 2^32.
- Each of 64 rounds applies `x ^= x << 13`, `x ^= x >> 17`, `x ^= x << 5`, then adds `0x9E3779B9 + round`, modulo 2^32.
- HLSL compilation uses strictness and optimization level 3. The source is embedded and fixed; no downloaded/user-provided shader or include is accepted.
- Each measured dispatch is bracketed by GPU timestamp queries inside a timestamp-disjoint interval. Duration is `(stop - start) / frequency × 1000` milliseconds.
- Shader compilation, constant upload, copy, readback and CPU verification are outside the measured GPU interval. Readback verification happens between samples, so the result is not a continuous saturated-throughput measurement.

All output elements are checked against the unsigned CPU calculation after each measured dispatch. The implementation rejects a disjoint clock, zero frequency, non-increasing timestamps, non-finite/non-positive duration, incomplete sample set or incorrect output. It also refuses to export a measurement unless the adapter is hardware and both Direct3D creation and compute verification passed. Raw samples retain execution order; the median/minimum/maximum use a sorted copy.

Close unrelated GPU work when comparing runs. Keep the same scenario/version, adapter, Windows/driver, power mode and operating conditions. Repeat across sessions and inspect dispersion. This short check does not collect temperature, clock, power, background load or throttling telemetry; these uncontrolled factors limit comparisons. A lower duration here must not be advertised as a percentage gain in a game or AI application.

## Selection, isolation and interruption

The user enumerates adapters, selects one, then requests a run. No GPU workload starts merely by opening the application or this screen. Enumeration and execution use a separate instance of the application's own executable, with a fixed JSON protocol on redirected local pipes. No arbitrary executable, command, script, model or shader path is accepted.

The worker opens the selected DXGI adapter again and verifies its name, model IDs, memory, driver and session-local LUID. An adapter removed/reordered/restarted between selection and execution causes an explicit inconclusive result rather than substitution. The LUID is transported only in the private parent/worker protocol and is never put in the user-facing export. There is no automatic fallback to WARP or to a different GPU.

The parent uses a 45-second global deadline, and the worker has its own watchdog if the parent exits unexpectedly. Cancel/close kills its own worker process tree; cleanup waits at most another three seconds for exit. Stream sizes and request size are bounded. GPU query/readback polling has a five-second per-pass deadline; a native call that stops responding is contained by the process deadline. Windows' own GPU recovery mechanism is not disabled. This is a short diagnostic workload, not an overclocking or firmware tool.

Outcomes are `passed`, `not_available`, `inconclusive`, `not_run`, `cancelled` and `timed_out`. A failure can be caused by the environment, driver, remote session, resource availability or a test implementation issue. It is never converted into a blanket statement that all games or AI programs are incompatible.

## Reports and privacy

Automatic tests export a separate `automatic_gpu_tests` report, schema 1/test version 1, with UTC time and application version. Existing configuration schema 1 and guided observation schema 2 are unchanged. Manual statements and measured results remain distinct.

The graphical-information checkbox controls name, driver version, dedicated memory and CUDA device details. PCI vendor/device IDs are an additional opt-in. Deselecting graphical details retains the test outcomes and timings, so an anonymized timing still describes a hardware or software execution. Index, LUID, UUID, serials, full device paths, account/PC names and native exception messages are never exported. Fixed allowlisted diagnostic codes replace native messages. The preview shows the content selected for copying/saving; nothing is uploaded automatically.

## Verification and release gates

The core test suite checks privacy filters, culture-independent JSON, all 14 catalogs, exact measurement statistics, invalid values, incomplete samples and the prohibition on WARP GPU scores. UI smoke tests inject results to exercise explicit selection, duplicate-run prevention, cancellation with a late result, close during execution, sharing options and language/RTL behavior.

On Windows, `--gpu-native-smoke` uses the real worker, compiler, device, dispatch and readback with WARP. WARP is only an explicit developer test path. It returns software status and no timing measurements. A separate CI step runs the worker from the packaged single-file executable, to cover that launch path. These checks must pass before publication; they are not evidence about a physical GPU or the hardware timestamp path.

Before claiming a card has been tested, run the exported binary on that exact model/variant and retain the report with Windows/driver context and observed limitations. Start with the available GTX 1060 after confirming its memory variant, then obtain evidence for the other scope families. Native Windows UI, physical GPU timing and legacy CUDA matching remain hardware acceptance tasks; a Linux cross-build cannot satisfy them.

## Next application-specific extensions

The first version provides a measurement/report foundation. Real AI and gaming measurements need separate versioned adapters and known workloads:

- AI: a pinned, licensed local model and engine, verified GPU offload, prompt-processing and generation speeds, memory, output correctness and quality.
- Games: an explicit supported title/version or captured process, repeatable scene/settings, rendered frame times, average FPS and a defined low-percentile calculation.
- Comparison: matched inputs/settings, repeated baseline/modified runs, dispersion, quality and latency tradeoffs.

Do not automatically download models, scan installed games, inject DLLs, attach to protected games, or infer broad compatibility from the current tests. Those features require their own implementation and validation under the [AI/gaming roadmap](https://github.com/FaipL/Grelecy/blob/main/docs/AI_GAMING_ROADMAP.md).

## Primary API references

- [DXGI adapter description](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)
- [D3D11CreateDevice and explicit adapter rules](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-d3d11createdevice)
- [Direct3D timestamp and disjoint queries](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_query)
- [Timestamp-disjoint result](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ns-d3d11-d3d11_query_data_timestamp_disjoint)
- [Microsoft's D3D11 ABI header](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/d3d11.h)
- [CUDA driver version API](https://docs.nvidia.com/cuda/cuda-driver-api/group__CUDA__VERSION.html)
- [CUDA device management and LUID matching](https://docs.nvidia.com/cuda/cuda-driver-api/group__CUDA__DEVICE.html)
- [.NET process termination](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)
