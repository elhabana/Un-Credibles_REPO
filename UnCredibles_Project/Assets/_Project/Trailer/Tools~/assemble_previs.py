"""Assembles the trailer previs from the recorded takes, following previs_edl.json.

Usage (from anywhere):  python assemble_previs.py [version]
Reads the takes from ~/Downloads/UN-CREDIBLES_Previs_Tomas and writes to its Previs/ folder:
  UNCREDIBLES_Previs_<version>.mp4         clean cut with the music and SFX mix of the EDL (or the animatic's audio)
  UNCREDIBLES_Previs_<version>_review.mp4  same, with shot number, label and timecode burnt in
  UNCREDIBLES_Previs_<version>_vs_animatica.mp4  animatic (left) next to the previs (right)
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TAKES = os.path.join(os.path.expanduser("~"), "Downloads", "UN-CREDIBLES_Previs_Tomas")
OUT = os.path.join(TAKES, "Previs")
WORK = os.path.join(OUT, "_segments")


def run(args):
    subprocess.run(["ffmpeg", "-v", "error", "-y", *args], check=True)


def encode_args(fps):
    return ["-r", str(fps), "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-pix_fmt", "yuv420p", "-an"]


def segment(shot, index, edl):
    w, h, fps = edl["width"], edl["height"], edl["fps"]
    frames = round(shot["dur"] * fps)
    out = os.path.join(WORK, f"{index:02d}_{shot['shot']}.mp4")
    fit = f"scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1"
    if "split" in shot:
        inputs = []
        # Each panel is a take name (sharing the shot's 'in') or {take, in}.
        for panel in shot["split"]:
            take, start = (panel, shot["in"]) if isinstance(panel, str) else (panel["take"], panel["in"])
            inputs += ["-ss", str(start), "-t", str(shot["dur"]), "-i", os.path.join(TAKES, take)]
        half = f"scale={w // 2}:{h // 2},setsar=1"
        graph = ";".join(f"[{i}:v]{half}[v{i}]" for i in range(4))
        graph += f";[v0][v1][v2][v3]xstack=inputs=4:layout=0_0|w0_0|0_h0|w0_h0,fps={fps}[v]"
        run([*inputs, "-filter_complex", graph, "-map", "[v]", "-frames:v", str(frames), *encode_args(fps), out])
        return out
    speed = shot.get("speed", 1.0)
    source = shot["dur"] * speed
    vf = f"setpts=PTS/{speed},{fit},fps={fps}" if speed != 1.0 else f"{fit},fps={fps}"
    run(["-ss", str(shot["in"]), "-t", f"{source + 0.2:.3f}", "-i", os.path.join(TAKES, shot["take"]),
         "-vf", vf, "-frames:v", str(frames), *encode_args(fps), out])
    return out


def build_audio(edl, total, out):
    """Mixes the music bed and the SFX placed at their trailer times into one stereo WAV."""
    audio = edl["audio"]
    folder = os.path.join(TAKES, "Audio")
    stereo = "aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo"
    inputs, chains, labels = [], [], []

    music = audio["music"]
    inputs += ["-ss", str(music.get("in", 0.0)), "-i", os.path.join(folder, music["file"])]
    chain = f"[0:a]{stereo},volume={music.get('gain_db', 0)}dB"
    for start, end, db in music.get("duck", []):
        chain += f",volume={db}dB:enable='between(t,{start},{end})'"
    if "fade_out" in music:
        chain += f",afade=t=out:st={music['fade_out'][0]}:d={music['fade_out'][1]}"
    delay = int(music.get("at", 0.0) * 1000)
    chains.append(chain + f",adelay={delay}:all=1[m]")
    labels.append("[m]")

    for i, sfx in enumerate(audio["sfx"], start=1):
        inputs += ["-i", os.path.join(folder, sfx["file"])]
        chain = f"[{i}:a]{stereo},atrim=start={sfx.get('in', 0.0)}"
        if "dur" in sfx:
            chain += f":duration={sfx['dur']}"
        chain += f",asetpts=PTS-STARTPTS,volume={sfx.get('gain_db', 0)}dB"
        if "fade" in sfx and "dur" in sfx:
            fade = sfx["fade"]
            chain += f",afade=t=in:d={min(fade, .15)},afade=t=out:st={max(0.0, sfx['dur'] - fade)}:d={fade}"
        chains.append(chain + f",adelay={int(sfx['at'] * 1000)}:all=1[s{i}]")
        labels.append(f"[s{i}]")

    graph = ";".join(chains)
    graph += f";{''.join(labels)}amix=inputs={len(labels)}:duration=longest:normalize=0," \
             f"alimiter=limit=0.89,apad,atrim=0:{total:.3f}[a]"
    run([*inputs, "-filter_complex", graph, "-map", "[a]", "-c:a", "pcm_s16le", out])


def main():
    version = sys.argv[1] if len(sys.argv) > 1 else "v01"
    with open(os.path.join(HERE, "previs_edl.json"), encoding="utf8") as f:
        edl = json.load(f)
    os.makedirs(WORK, exist_ok=True)
    fps = edl["fps"]

    parts, starts, t = [], [], 0.0
    for i, shot in enumerate(edl["shots"]):
        print(f"[{shot['shot']}] {shot['label']}")
        parts.append(segment(shot, i, edl))
        starts.append(t)
        t += shot["dur"]
    print(f"Total: {t:.2f} s")

    listing = os.path.join(WORK, "list.txt")
    with open(listing, "w", encoding="utf8") as f:
        for part in parts:
            f.write(f"file '{part.replace(os.sep, '/')}'\n")
    video = os.path.join(WORK, "video.mp4")
    run(["-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", video])

    # Sound: the mix described in the EDL, or the animatic's track as a guide when there is none.
    sound = edl["guide_audio"]
    if "audio" in edl:
        sound = os.path.join(WORK, "mix.wav")
        build_audio(edl, t, sound)
    clean = os.path.join(OUT, f"UNCREDIBLES_Previs_{version}.mp4")
    run(["-i", video, "-i", sound, "-map", "0:v", "-map", "1:a?", "-c:v", "copy",
         "-c:a", "aac", "-b:a", "192k", "-t", f"{t:.3f}", clean])

    # Review copy: shot number + label (top left) and timecode (top right), like the animatic.
    font = os.path.join(WORK, "arial.ttf")
    if not os.path.exists(font):
        shutil.copy(r"C:\Windows\Fonts\arial.ttf", font)
    draws = []
    for shot, start in zip(edl["shots"], starts):
        end = start + shot["dur"]
        text = f"{shot['shot']}  {shot['label']}".replace("'", "").replace(":", "\\:")
        draws.append(f"drawtext=fontfile=arial.ttf:text='{text}':x=24:y=20:fontsize=30:fontcolor=white:"
                     f"box=1:boxcolor=black@0.55:boxborderw=8:enable='between(t,{start:.3f},{end - 0.001:.3f})'")
    draws.append("drawtext=fontfile=arial.ttf:text='%{pts\\:hms}':x=w-tw-24:y=20:fontsize=30:fontcolor=yellow:"
                 "box=1:boxcolor=black@0.55:boxborderw=8")
    review = os.path.join(OUT, f"UNCREDIBLES_Previs_{version}_review.mp4")
    cwd = os.getcwd()
    os.chdir(WORK)
    try:
        run(["-i", clean, "-vf", ",".join(draws), "-c:v", "libx264", "-preset", "medium", "-crf", "19",
             "-pix_fmt", "yuv420p", "-c:a", "copy", review])
    finally:
        os.chdir(cwd)

    versus = os.path.join(OUT, f"UNCREDIBLES_Previs_{version}_vs_animatica.mp4")
    run(["-i", edl["guide_audio"], "-i", clean, "-filter_complex",
         f"[0:v]scale=960:540,setsar=1,fps={fps}[a];[1:v]scale=960:540,setsar=1[b];[a][b]hstack=2[v]",
         "-map", "[v]", "-map", "1:a?", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-t", f"{t:.3f}", versus])
    print(clean)
    print(review)
    print(versus)


if __name__ == "__main__":
    main()
