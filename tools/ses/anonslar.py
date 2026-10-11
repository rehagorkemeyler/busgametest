"""
Otobüs içi Türkçe durak anonsları: her durak için "Sıradaki durak: X." ve "Sıradaki durak: X. Son durak.", hat sonu anonsu
ve anons öncesi gong. Çıktı: AnkaraBusSimulator/Assets/_Project/Resources/Anonslar/ (AnonsSistemi adla yükler).
Dosya adı durak adının sadeleşmiş hali (anons_adi: küçük harf, Türkçe harfler Latin, boşluk → _), C# tarafı aynı kuralı kullanır.

Ses: Piper TTS, tr_TR-dfki-medium (tools/ses/anons_tr.py ile aynı ses ve lisans, docs/KREDILER.md). 48 kHz mono 16 bit.
Durak eklenince DURAKLAR'a ekleyip yeniden çalıştırın.

Kullanım:
    pip install piper-tts
    python tools/ses/anonslar.py --model tr_TR-dfki-medium.onnx --out AnkaraBusSimulator/Assets/_Project/Resources/Anonslar
"""
import argparse
import math
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

DURAKLAR = ["Kızılay AVM", "Meclis", "Kuğulu Park", "Cinnah", "Atakule",   # Hat 1
            "Kızılay", "Sıhhiye", "Opera", "Ulus"]                         # Hat 2
# TTS'in doğru okuması için yazılış (ekranda/dosya adında asıl ad kullanılır)
# (Whisper ile denendi: "Sıradaki durak: Ulus" → "buluz", virgülle doğru; AVM, Atakule, Cinnah, Sıhhiye ayrı yazılınca doğru)
OKUNUS = {"Kızılay AVM": "Kızılay Ave Me", "Atakule": "Ata kule", "Cinnah": "Cinnâh", "Sıhhiye": "Sıhiye"}
HAT_SONU = "Son durağa geldik. İnerken eşyalarınızı unutmayınız. İyi günler dileriz."
SR = 48000
HEDEF_RMS = 0.12


def anons_adi(ad):
    tablo = str.maketrans("çğıöşüÇĞİÖŞÜ", "cgiosuCGIOSU")
    return "_".join(ad.translate(tablo).lower().split())


def oku(yol):
    with wave.open(yol) as w:
        sr, ch = w.getframerate(), w.getnchannels()
        a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    return a.reshape(-1, ch).mean(1), sr


def yaz(yol, a):
    a = np.clip(a, -0.98, 0.98)
    with wave.open(yol, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((a * 32767).astype(np.int16).tobytes())


def konus(model, metin):
    with tempfile.TemporaryDirectory() as tmp:
        ham = os.path.join(tmp, "s.wav")
        subprocess.run([os.path.join(os.path.dirname(sys.executable), "piper"), "-m", model, "--length_scale", "1.0",
                        "--sentence_silence", "0.35", "-f", ham], input=metin.encode(), check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        a, sr = oku(ham)
    esik = np.abs(a).max() * 0.02
    idx = np.nonzero(np.abs(a) > esik)[0]
    pay = int(0.05 * sr)
    a = a[max(0, idx[0] - pay): idx[-1] + pay]
    t = np.arange(0, len(a) / sr, 1 / SR)
    a = np.interp(t, np.arange(len(a)) / sr, a)
    konusan = a[np.abs(a) > esik]
    a *= HEDEF_RMS / max(float(np.sqrt(np.mean(konusan ** 2))), 1e-6)
    # anons hoparlörü: 250 Hz altı ve 5 kHz üstü yumuşakça kısılır (basit tek kutuplu süzgeçler)
    a = yuksek_gecir(alcak_gecir(a, 5000), 250)
    yumusak = int(0.01 * SR)
    a[:yumusak] *= np.linspace(0, 1, yumusak)
    a[-yumusak:] *= np.linspace(1, 0, yumusak)
    return a


def alcak_gecir(a, fc):
    k = 1 - math.exp(-2 * math.pi * fc / SR)
    out = np.empty_like(a)
    y = 0.0
    for i, x in enumerate(a):
        y += k * (x - y)
        out[i] = y
    return out


def yuksek_gecir(a, fc):
    return a - alcak_gecir(a, fc)


def gong():
    """İki tonlu "ding-dong" (Mi5 → Do5), çan benzeri harmoniklerle, 1,4 sn."""
    t = np.arange(int(1.4 * SR)) / SR
    a = np.zeros_like(t)
    for bas, f in ((0.0, 659.25), (0.45, 523.25)):
        tt = t - bas
        on = tt >= 0
        zarf = np.where(on, np.exp(-np.clip(tt, 0, None) * 3.2), 0) * np.clip(tt * 200, 0, 1)
        ses = np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * 2 * f * tt) + 0.12 * np.sin(2 * np.pi * 3.01 * f * tt)
        a += zarf * ses
    return a / np.abs(a).max() * 0.45


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    yaz(os.path.join(args.out, "gong.wav"), gong())
    yaz(os.path.join(args.out, "hat_sonu.wav"), konus(args.model, HAT_SONU))
    for ad in DURAKLAR:
        okunus = OKUNUS.get(ad, ad)
        yaz(os.path.join(args.out, f"sonraki_{anons_adi(ad)}.wav"), konus(args.model, f"Sıradaki durak, {okunus}."))
        yaz(os.path.join(args.out, f"sonraki_son_{anons_adi(ad)}.wav"),
            konus(args.model, f"Sıradaki durak, {okunus}. Son durak."))
        print("yazıldı:", ad)


if __name__ == "__main__":
    main()
