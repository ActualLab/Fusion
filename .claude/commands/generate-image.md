---
allowed-tools: Bash
description: Generate an image using Gemini API
argument-hint: [output-file] [description]
---

Generate an image using Google's Gemini API based on the user's description.

## Instructions

1. Parse $ARGUMENTS to extract:
   - Output filename (first argument, default: `generated_image.png`)
   - Image description (remaining arguments, required)

2. Use the Gemini API to generate an image:
   - API Key is available in environment variable: `Claude_GeminiAPIKey`
   - Use the Imagen 3 model (`imagen-3.0-generate-002`) for image generation
   - API endpoint: `https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-002:predict`

3. Make the API call using curl:
   ```bash
   curl -s "https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-002:predict?key=$Claude_GeminiAPIKey" \
     -H "Content-Type: application/json" \
     -d '{
       "instances": [{"prompt": "<USER_PROMPT>"}],
       "parameters": {"sampleCount": 1}
     }'
   ```

4. The response will contain base64-encoded image data in `predictions[0].bytesBase64Encoded`

5. Decode and save the image:
   ```bash
   echo "<base64_data>" | base64 -d > <output_filename>
   ```

6. Report success with the output filename, or report any errors from the API.

## Arguments

$ARGUMENTS
