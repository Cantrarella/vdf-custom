# Windows AI bundle

Windows release packaging runs `bundle-ai.ps1 -OutputDirectory outputGUI` after publishing the GUI and before creating its ZIP. The script adds ONNX Runtime 1.23.2, the SHA256-pinned DINOv2-small quantized model, version marker, and upstream licenses to `ai/`. These binary assets are downloaded at packaging time rather than stored in Git.

Extract the entire ZIP and keep `ai/` beside `VDF.GUI.exe`. A complete bundle is read directly even when the application directory is read-only. Missing or incomplete bundles retain the existing writable-state download behavior. AI recognition rules and model version are unchanged.

The model SHA256 is `3afdc8bc63b50558d6e5770f5b799bb82455c2311183a2de43803f343a29d917`. Packaging fails when it does not match. Runtime and model versions in this script must stay synchronized with `AiComponents.cs` and the ONNX managed package.
