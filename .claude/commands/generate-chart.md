---
allowed-tools: Read, Write, Bash(python3:*)
description: Generate charts using Python 3 (matplotlib, seaborn, plotly, pandas, numpy)
argument-hint: [output-file] [description]
---

# Generate Chart

Generate a chart based on the user's description.

## Instructions

1. Parse $ARGUMENTS to extract:
   - Output filename (first argument, default: `chart.png`)
   - Chart description (remaining arguments, required)

2. Create a Python script that generates the requested chart
3. Save the script to a temporary file (e.g., `/tmp/chart_script.py`)
4. Execute the script using `python3`
5. Save the output image to the specified output file

## Available Python Packages

The following packages are pre-installed (see @claude.Dockerfile for details):

- **matplotlib** - Standard plotting library
- **seaborn** - Statistical data visualization
- **plotly** - Interactive charts (use kaleido for static export)
- **pandas** - Data manipulation and analysis
- **numpy** - Numerical computing
- **pillow** - Image processing
- **kaleido** - Static image export for Plotly

## Output Guidelines

- Default output format: PNG
- Default filename: `chart.png` (saved in current directory)
- For Plotly charts, use `fig.write_image()` with kaleido backend
- Always include a title and axis labels where appropriate
- Use clear, readable fonts and colors

## Example Script Structure

```python
import matplotlib.pyplot as plt
import numpy as np

output_file = 'chart.png'  # Use the output-file from arguments

# Generate data
x = np.linspace(0, 10, 100)
y = np.sin(x)

# Create figure
plt.figure(figsize=(10, 6))
plt.plot(x, y)
plt.title('Chart Title')
plt.xlabel('X Axis')
plt.ylabel('Y Axis')
plt.grid(True)
plt.savefig(output_file, dpi=150, bbox_inches='tight')
plt.close()

print(f'Chart saved to {output_file}')
```

After generating the chart, inform the user of the output file location.
