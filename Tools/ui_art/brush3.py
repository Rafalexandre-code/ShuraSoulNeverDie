"""Sumi-e brush stroke model shared by the UI generator.

A stroke is an ink body around a path (blunt pressed entry, ragged edges) multiplied by
dry-brush streaks that run along the stroke and widen toward its end (the brush runs out
of ink), mapped in stroke coordinates (u along, v across) so streaks follow curves.
"""
import math

import numpy as np
from scipy import ndimage as ndi
from scipy.spatial import cKDTree


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def stroke(h, w, path, width, rng, dryness=0.6, steps=1500, entry=0.06, exit_=0.25, end_width=0.65,
           pressure=None, edge_rough=0.045, streak_len=40.0, density=1.0, tail_split=0.5):
    t = np.linspace(0, 1, steps)
    pts = np.array([path(v) for v in t])
    tang = np.gradient(pts, axis=0)
    tang /= np.linalg.norm(tang, axis=1, keepdims=True) + 1e-9
    normal = np.stack([-tang[:, 1], tang[:, 0]], axis=1)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    arclen = np.concatenate([[0], np.cumsum(seg)])
    L = arclen[-1] + 1e-6

    rise = 0.5 + 0.5 * np.sqrt(smoothstep(0, entry, t))
    fall = end_width + (1 - end_width) * (1 - smoothstep(1 - exit_, 1, t))
    prof = rise * fall
    if pressure is not None:
        prof = prof * pressure(t)
    # The edge wobbles independently on each side.
    wob_l = 1 + ndi.gaussian_filter1d(rng.normal(0, 1, steps), 12) * edge_rough * 3
    wob_r = 1 + ndi.gaussian_filter1d(rng.normal(0, 1, steps), 12) * edge_rough * 3
    half = width * 0.5 * prof

    pad = int(width) + 4
    x0 = max(0, int(pts[:, 0].min()) - pad)
    x1 = min(w, int(pts[:, 0].max()) + pad)
    y0 = max(0, int(pts[:, 1].min()) - pad)
    y1 = min(h, int(pts[:, 1].max()) + pad)
    out = np.zeros((h, w))
    if x1 <= x0 or y1 <= y0:
        return out
    yy, xx = np.mgrid[y0:y1, x0:x1]
    P = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], axis=1)
    tree = cKDTree(pts)
    dist, idx = tree.query(P, k=1)
    d = P - pts[idx]
    v = np.einsum("ij,ij->i", d, normal[idx])
    along = np.einsum("ij,ij->i", d, tang[idx])
    u = t[idx]
    hw = np.where(v >= 0, half[idx] * wob_r[idx], half[idx] * wob_l[idx])
    vn = v / np.maximum(hw, 1e-3)

    # Rounded caps: beyond the ends, fade by distance along the tangent.
    cap = np.ones_like(u)
    start_mask = (idx == 0) & (along < 0)
    end_mask = (idx == steps - 1) & (along > 0)
    cap[start_mask] = smoothstep(1.0, 0.6, np.hypot(along[start_mask] / np.maximum(hw[start_mask], 1), vn[start_mask]))
    cap[end_mask] = smoothstep(0.35, 0.0, along[end_mask] / np.maximum(hw[end_mask], 1))

    # Fine edge roughness (paper tooth).
    rough = rng.normal(0, 1, P.shape[0]).reshape(yy.shape)
    rough = ndi.gaussian_filter(rough, 1.6).ravel() * edge_rough * 3.0
    body = smoothstep(1.0 + rough, 0.86 + rough, np.abs(vn)) * cap

    # Dry streaks: a noise field in (arc length, across) space, long along the stroke.
    NV = 256
    NU = max(64, int(L / 2))
    field = rng.normal(0, 1, (NV, NU))
    field = ndi.gaussian_filter(field, (1.2, streak_len / 2), mode="wrap")
    field /= field.std() + 1e-9
    ui = np.clip((arclen[idx] / L * (NU - 1)).astype(int), 0, NU - 1)
    vi = np.clip(((vn * 0.5 + 0.5) * (NV - 1)).astype(int), 0, NV - 1)
    s = field[vi, ui]
    # Dryness grows along the stroke and at the edges; the tail splits into separate hairs.
    dry_amt = dryness * (0.15 + 0.85 * smoothstep(0.1, 1.0, u)) + 0.35 * dryness * np.abs(vn) ** 3
    dry_amt += tail_split * dryness * smoothstep(0.82, 1.0, u)
    thresh = 1.9 - 2.6 * dry_amt
    ink = smoothstep(thresh - 0.25, thresh + 0.25, -s)
    ink = 1 - ink
    tone = density * (0.86 + 0.14 * smoothstep(0.5, 0.0, u)) * (0.94 + 0.06 * np.clip(s, -1, 1))
    # The brush lifts off on a slant: one side of the stroke ends a little before the other.
    lift = 1.0 - 0.07 * (0.5 + 0.5 * np.clip(vn, -1, 1))
    tail = smoothstep(lift, lift - 0.035, u)
    a = (body * ink * tone * tail).reshape(yy.shape)
    a = ndi.gaussian_filter(a, 0.5)
    out[y0:y1, x0:x1] = np.clip(a, 0, 1)
    return out


def line(x0, y0, x1, y1, wob, rng, bow=0.0):
    s = rng.uniform(0, 100)
    return lambda t: (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t + math.sin(t * math.pi) * bow + math.sin(t * 6 + s) * wob)
