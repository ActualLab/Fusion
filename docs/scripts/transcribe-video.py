import argparse
import json
from pathlib import Path

from faster_whisper import WhisperModel


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--video-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--model", default="large-v3-turbo")
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--compute-type", default="int8")
    parser.add_argument("--prompt-file", type=Path)
    parser.add_argument("--paragraph-seconds", type=float, default=35)
    return parser.parse_args()


def format_timestamp(seconds: float) -> str:
    total_seconds = max(0, round(seconds))
    hours, remainder = divmod(total_seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    if hours:
        return f"{hours:02}:{minutes:02}:{seconds:02}"
    return f"{minutes:02}:{seconds:02}"


def group_segments(segments: list[dict], paragraph_seconds: float) -> list[dict]:
    paragraphs: list[dict] = []
    current: dict | None = None
    for segment in segments:
        if current is None or segment["end"] - current["start"] > paragraph_seconds:
            current = {
                "start": segment["start"],
                "end": segment["end"],
                "text": segment["text"],
            }
            paragraphs.append(current)
        else:
            current["end"] = segment["end"]
            current["text"] += " " + segment["text"]
    return paragraphs


def main() -> None:
    args = parse_args()
    initial_prompt = args.prompt_file.read_text(encoding="utf-8") if args.prompt_file else None
    model = WhisperModel(args.model, device=args.device, compute_type=args.compute_type)
    result, info = model.transcribe(
        str(args.input),
        language="en",
        beam_size=5,
        vad_filter=True,
        condition_on_previous_text=True,
        initial_prompt=initial_prompt,
    )
    segments = [
        {
            "start": segment.start,
            "end": segment.end,
            "text": segment.text.strip(),
        }
        for segment in result
    ]

    args.output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "input": str(args.input),
        "videoId": args.video_id,
        "model": args.model,
        "language": info.language,
        "languageProbability": info.language_probability,
        "duration": info.duration,
        "segments": segments,
    }
    args.output.with_suffix(".json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    markdown: list[str] = []
    for paragraph in group_segments(segments, args.paragraph_seconds):
        timestamp = format_timestamp(paragraph["start"])
        url = f"https://www.youtube.com/watch?v={args.video_id}&t={round(paragraph['start'])}s"
        markdown.append(f"[{timestamp}]({url})")
        markdown.append(paragraph["text"])
        markdown.append("")
    args.output.with_suffix(".md").write_text(
        "\n".join(markdown),
        encoding="utf-8",
        newline="\n",
    )


if __name__ == "__main__":
    main()
