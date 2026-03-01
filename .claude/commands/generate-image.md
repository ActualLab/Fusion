---
allowed-tools: Bash
description: Generate an image using Gemini API
argument-hint: [output-file] [description]
---

Generate an image using Google's Gemini API based on the user's description.

## Instructions

1. Parse $ARGUMENTS to extract:
   - Output filename (first argument ending in `.png`, `.jpg`, or `.jpeg`; default: `generated_image.png`)
   - Image description (remaining arguments, required)

2. Use the Gemini API to generate an image:
   - API Key is available in environment variable: `Claude_Gemini_API_Key`
   - Use the Gemini 3 Pro Image model (`gemini-3-pro-image-preview`)
   - API endpoint: `https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent`

3. Run the following script (replace `<OUTPUT_FILE>` and `<USER_PROMPT>` with actual values):

```bash
curl -s "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent?key=$Claude_Gemini_API_Key" \
  -H "Content-Type: application/json" \
  -d '{
    "contents": [{
      "parts": [{"text": "Generate an image of: <USER_PROMPT>"}]
    }],
    "generationConfig": {
      "responseModalities": ["TEXT", "IMAGE"]
    }
  }' > /tmp/gemini_response.json 2>&1

python3 << 'EOF'
import json
import base64
import sys

output_file = "<OUTPUT_FILE>"

with open('/tmp/gemini_response.json', 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

try:
    data = json.loads(content)
    if 'error' in data:
        print(f"API Error: {data['error'].get('message', data['error'])}")
        sys.exit(1)

    for part in data.get('candidates', [{}])[0].get('content', {}).get('parts', []):
        if 'inlineData' in part:
            img_data = part['inlineData']['data']
            mime_type = part['inlineData'].get('mimeType', 'image/png')
            with open(output_file, 'wb') as img_file:
                img_file.write(base64.b64decode(img_data))
            print(f"Image saved successfully to {output_file}")
            sys.exit(0)

    print("No image found in response")
    sys.exit(1)
except json.JSONDecodeError as e:
    print(f"JSON parse error: {e}")
    print(f"Response preview: {content[:500]}")
    sys.exit(1)
EOF
```

4. Report success with the output filename, or report any errors from the API.

## Arguments

$ARGUMENTS
