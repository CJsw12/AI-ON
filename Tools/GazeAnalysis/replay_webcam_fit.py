"""Replays saved webcam gaze fits to compare calculations and tune ResNetAngleGain.

The game (editor / development build) saves every screen fit and accuracy check to
  %USERPROFILE%\\AppData\\LocalLow\\<company>\\<product>\\gaze-fits\\webcam-*.json
Each sample is GazeMapper.Features (13 values) followed by gaze yaw, gaze pitch, head x, head y, distance.

  python replay_webcam_fit.py <fit.json> <check.json> [--skip 1]

Fits on the screen-fit file, scores on the check file (ideally measured from another head position),
and prints the error for UnitEye's point and for the screen-plane point at several angle gains.
--skip drops the first check points (e.g. ones measured before the head was moved). Needs numpy.
"""
import argparse, json
import numpy as np

YAW, PITCH, HX, HY, DIST = 13, 14, 15, 16, 17


def load(path, skip=0):
    d = json.load(open(path, encoding="utf-8"))
    targets = np.array([p["target"] for p in d["points"]], float)[skip:]
    medians = np.array([np.median(np.array(p["samples"], float), 0) for p in d["points"]])[skip:]
    return targets, medians


def plane(s, gain, axis):
    angle, head = (s[:, YAW], s[:, HX]) if axis == 0 else (s[:, PITCH], s[:, HY])
    return head + s[:, DIST] * np.tan(np.clip(angle / gain, -1.2, 1.2))


def score(xf, tf, xc, tc, pixels):
    w = np.linalg.lstsq(np.c_[xf, np.ones_like(xf)], tf, rcond=None)[0]
    err = lambda x, t: np.abs(x * w[0] + w[1] - t).mean() * pixels
    return err(xf, tf), err(xc, tc)


def main():
    a = argparse.ArgumentParser()
    a.add_argument("fit"); a.add_argument("check"); a.add_argument("--skip", type=int, default=0)
    a = a.parse_args()
    tf, sf = load(a.fit)
    tc, sc = load(a.check, a.skip)
    if sf.shape[1] <= DIST:
        raise SystemExit("These files predate the raw yaw/pitch/head columns; measure again.")
    for axis, name, pixels in ((0, "horizontal", 1280), (1, "vertical", 900)):
        print(f"== {name}: error in px at the fit pose / at the check pose")
        print("  UnitEye point   %5.0f / %5.0f" % score(sf[:, axis], tf[:, axis], sc[:, axis], tc[:, axis], pixels))
        for gain in (1.0, .6, .5, .45, .4, .37, .35, .3, .25):
            print(f"  plane g={gain:<4}  %5.0f / %5.0f" % score(plane(sf, gain, axis), tf[:, axis], plane(sc, gain, axis), tc[:, axis], pixels))
        shift = np.median(sc[:, HX if axis == 0 else HY]) - np.median(sf[:, HX if axis == 0 else HY])
        print(f"  head moved {shift * 100:+.1f} cm between the files")


if __name__ == "__main__":
    main()
