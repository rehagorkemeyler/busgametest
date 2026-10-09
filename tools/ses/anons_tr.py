"""
Otobüs içi Türkçe ses: yolcunun durak isteği ("Durakta inecek var!"). BMC ses paketindeki stop.wav Rusçaydı
("На остановке, будьте добры!" — "Durakta, lütfen!"); aynı dosya adıyla (Unity bağlantıları bozulmadan) değiştirilir.

Ses: Piper TTS, tr_TR-dfki-medium (https://huggingface.co/rhasspy/piper-voices; veri kümesi dfki-ot-data,
CC BY-NC-SA 4.0 — ticari olmayan kullanım, docs/KREDILER.md). Çıktı eski dosyanın biçiminde: 48 kHz, mono, 16 bit;
seviye eski sesin RMS'ine eşitlenir, baş/son sessizlik kırpılır, kısa yumuşak giriş/çıkış.

Kullanım:
    pip install piper-tts
    python tools/ses/anons_tr.py --model tr_TR-dfki-medium.onnx --out AnkaraBusSimulator/Assets/_Project/Buses/BMC_Procity_12LF/Sounds/stop.wav
"""
import argparse
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

METIN = "Durakta inecek var!"
HEDEF_SR = 48000
HEDEF_RMS = None  # None: eski dosyanın RMS'i


def oku(yol):
    with wave.open(yol) as w:
        sr, ch, sw = w.getframerate(), w.getnchannels(), w.getsampwidth()
        a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    assert sw == 2
    return a.reshape(-1, ch).mean(1), sr


def rms(a):
    return float(np.sqrt(np.mean(a ** 2)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--metin", default=METIN)
    args = ap.parse_args()

    hedef = rms(oku(args.out)[0][oku(args.out)[0] != 0]) if os.path.exists(args.out) else 0.08
    with tempfile.TemporaryDirectory() as tmp:
        ham = os.path.join(tmp, "ses.wav")
        subprocess.run([os.path.join(os.path.dirname(sys.executable), "piper"), "-m", args.model,
                        "--length_scale", "0.95", "-f", ham], input=args.metin.encode(), check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        a, sr = oku(ham)
    # sessizliği kırp (eşik tepe değerin %2'si), 40 ms pay bırak
    esik = np.abs(a).max() * 0.02
    idx = np.nonzero(np.abs(a) > esik)[0]
    pay = int(0.04 * sr)
    a = a[max(0, idx[0] - pay): idx[-1] + pay]
    # 48 kHz'e doğrusal ara değerleme (konuşma bandı için yeterli)
    t = np.arange(0, len(a) / sr, 1 / HEDEF_SR)
    a = np.interp(t, np.arange(len(a)) / sr, a)
    # seviye: konuşan kısmın RMS'i eski sesinkine
    konusan = a[np.abs(a) > esik]
    a *= hedef / max(rms(konusan), 1e-6)
    a = np.clip(a, -0.98, 0.98)
    yumusak = int(0.01 * HEDEF_SR)
    a[:yumusak] *= np.linspace(0, 1, yumusak)
    a[-yumusak:] *= np.linspace(1, 0, yumusak)
    with wave.open(args.out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(HEDEF_SR)
        w.writeframes((a * 32767).astype(np.int16).tobytes())
    print(f"yazıldı: {args.out} ({len(a) / HEDEF_SR:.2f} sn, \"{args.metin}\")")


if __name__ == "__main__":
    main()
