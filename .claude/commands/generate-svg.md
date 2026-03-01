---
allowed-tools: Bash
description: Generate an SVG using Gemini API
argument-hint: [output-file] [description]
---

Generate an SVG file using Google's Gemini 3.1 Pro Preview model based on the user's description.

## Instructions

1. Parse $ARGUMENTS to extract:
   - Output filename (first argument ending in `.svg`; default: `generated.svg`)
   - SVG description (remaining arguments, required)

2. Use the Gemini API to generate SVG:
   - API Key is available in environment variable: `Claude_Gemini_API_Key`
   - Use the Gemini 3.1 Pro Preview model (`gemini-3.1-pro-preview`)
   - API endpoint: `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-pro-preview:generateContent`

3. Run the following script (replace `<OUTPUT_FILE>` and `<USER_PROMPT>` with actual values):

```bash
curl -s "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-pro-preview:generateContent?key=$Claude_Gemini_API_Key" \
  -H "Content-Type: application/json" \
  -d '{
    "contents": [{
      "parts": [{"text": "Generate a clean, well-structured SVG image of: <USER_PROMPT>\n\nIMPORTANT: Output ONLY the raw SVG markup starting with <svg and ending with </svg>. Do not include any explanation, markdown fences, or other text. The SVG should be self-contained with inline styles, use viewBox for proper scaling, and be optimized for clarity."}]
    }],
    "generationConfig": {
      "responseModalities": ["TEXT"]
    }
  }' > /tmp/gemini_svg_response.json 2>&1

python3 << 'EOF'
import json
import re
import sys

output_file = "<OUTPUT_FILE>"

with open('/tmp/gemini_svg_response.json', 'r', encoding='utf-8', errors='replace') as f:
    content = f.read()

try:
    data = json.loads(content)
    if 'error' in data:
        print(f"API Error: {data['error'].get('message', data['error'])}")
        sys.exit(1)

    text_parts = []
    for part in data.get('candidates', [{}])[0].get('content', {}).get('parts', []):
        if 'text' in part:
            text_parts.append(part['text'])

    if not text_parts:
        print("No text found in response")
        sys.exit(1)

    full_text = '\n'.join(text_parts)

    # Extract SVG content - look for <svg...>...</svg>
    svg_match = re.search(r'(<svg[\s\S]*?</svg>)', full_text, re.DOTALL)
    if svg_match:
        svg_content = svg_match.group(1)
    else:
        print("No SVG markup found in response. Raw response:")
        print(full_text[:1000])
        sys.exit(1)

    with open(output_file, 'w', encoding='utf-8') as svg_file:
        svg_file.write(svg_content)
    print(f"SVG saved successfully to {output_file}")

except json.JSONDecodeError as e:
    print(f"JSON parse error: {e}")
    print(f"Response preview: {content[:500]}")
    sys.exit(1)
EOF
```

4. After saving, display the SVG file to the user using the Read tool so they can see the result.

5. Report success with the output filename, or report any errors from the API.

## Color and Font Scheme for Docs SVGs

When generating SVGs for the documentation site (`docs/` folder), use the following design system. See `docs/img/call-graph.svg` as a reference.

**Fonts:**
- Method/code text: `"SF Mono", "Fira Code", "Cascadia Code", Consolas, Menlo, Monaco, monospace` at 13px
- Labels/annotations: `Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif` at 11px, italic

**Background:**
- Light sky-blue gradient: `#f4f8fd` → `#eaf1fb` → `#e2ebf7` (diagonal, top-left to bottom-right)
- Rounded corners: `rx="10"`

**Node boxes (rounded rectangles):**
- Fill: gradient `#e8f0fc` → `#d4e3f7` (diagonal)
- Border: `#bcc8da`, 1px (or 1.5px for emphasis)
- Corner radius: `rx="12"`

**Text colors:**
- Primary text (method names): `#1a1a2e`
- Secondary text (parameters, labels): `#6b7084`

**Arrows/connections:**
- Stroke: `#8b90a8`, 1.5px, round linecap
- Arrowhead fill: `#8b90a8`
- "calls" labels: `#6b7084`, 11px, italic

## Arguments

$ARGUMENTS
