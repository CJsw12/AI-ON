"""Builds Assets/Resources/PlayerBody.png (the player's body under the webcam face) from Tools/art/PlayerBody-source.png.

    python Tools/make_player_body.py

The source is a generated studio photo on a plain light-grey background. This removes the background, cuts the
head off just below the chin (the webcam photo goes there) with a soft edge, and prints the face numbers that
GameController.PlayerPhoto.cs uses to size and place the body like a professor portrait (same face height).
"""
import os

import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Tools", "art", "PlayerBody-source.png")
OUTPUT = os.path.join(ROOT, "Assets", "Resources", "PlayerBody.png")
CUT_TOP, CUT_SOLID = 528, 556   # rows (source px): invisible above CUT_TOP, opaque from CUT_SOLID (chin is at ~522)

img = cv2.imread(SOURCE)
h, w = img.shape[:2]
border = np.concatenate([img[:12].reshape(-1, 3), img[:, :12].reshape(-1, 3), img[:, -12:].reshape(-1, 3)])
background = np.median(border, axis=0)
distance = np.linalg.norm(img.astype(np.float32) - background, axis=2)
alpha = np.clip((distance - 12) / 22, 0, 1)
# Keep only the figure (the largest region), fill pinholes, then soften the edge by a pixel.
mask = (alpha > .5).astype(np.uint8)
count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
figure = labels == 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA])
figure = cv2.morphologyEx(figure.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
alpha = np.where(figure, np.maximum(alpha, cv2.erode(figure.astype(np.uint8), np.ones((3, 3), np.uint8))), 0)
alpha = cv2.GaussianBlur(alpha.astype(np.float32), (3, 3), 0)

gray = cv2.equalizeHist(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY))
faces = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml").detectMultiScale(gray, 1.05, 5, minSize=(200, 200))
x, y, s, _ = max(faces, key=lambda f: f[2])

# Remove the grey fringe the background left on the edges (before the neck cut, which is not a background mix).
a = alpha[:, :, None]
colour = np.where(a > .02, (img - (1 - a) * background) / np.maximum(a, .02), 0)
rows = np.arange(h, dtype=np.float32)[:, None]
alpha *= np.clip((rows - CUT_TOP) / (CUT_SOLID - CUT_TOP), 0, 1)
out = np.dstack([np.clip(colour, 0, 255), alpha * 255]).astype(np.uint8)
cv2.imwrite(OUTPUT, out)
ys = np.nonzero(alpha.max(axis=1) > .1)[0]
print(f"wrote {os.path.relpath(OUTPUT, ROOT)}")
print(f"PortraitFaces.Entry({(x + s / 2) / w:.4f}f, {(y + s / 2) / h:.4f}f, {s / h:.4f}f, {(ys.max() + 1) / h:.4f}f, {w / h:.4f}f)")
