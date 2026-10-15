"""Bir noktadaki bütün yüzeyler (parça pkl'lerinden): python -m hat3.nokta x z [x z ...]"""
import sys
import numpy as np
from .denetim import ucgenler, agac, sorgu

def main():
    a = [float(v) for v in sys.argv[1:]]
    P = np.array(list(zip(a[0::2], a[1::2])))
    for desen in ("yol_*.pkl", "simge_*.pkl", "bina_*.pkl"):
        T, AD = ucgenler(desen)
        pi, ti, y = sorgu(agac(T), T, P)
        for k in range(len(P)):
            m = pi == k
            print(f"({P[k][0]:.0f},{P[k][1]:.0f}) {desen}: " + ", ".join(f"{AD[t]} {yy:.2f}" for t, yy in sorted(zip(ti[m], y[m]), key=lambda q: q[1])))

if __name__ == "__main__":
    main()
