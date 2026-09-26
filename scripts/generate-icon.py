#!/usr/bin/env python3
"""Write a monochrome multi-size ICO for the Windows executable."""

import struct
from pathlib import Path


def pixel(size: int, x: int, y: int) -> tuple[int, int, int, int]:
    margin = max(1, size // 16)
    radius = max(2, size // 8)
    inner = margin
    outer = size - 1 - margin

    def inside(px: int, py: int) -> bool:
        if px < inner or py < inner or px > outer or py > outer:
            return False
        corners = (
            (inner + radius, inner + radius, px < inner + radius and py < inner + radius),
            (outer - radius, inner + radius, px > outer - radius and py < inner + radius),
            (inner + radius, outer - radius, px < inner + radius and py > outer - radius),
            (outer - radius, outer - radius, px > outer - radius and py > outer - radius),
        )
        for cx, cy, active in corners:
            if active:
                return (px - cx) ** 2 + (py - cy) ** 2 <= radius * radius
        return True

    if not inside(x, y):
        return (0, 0, 0, 0)

    stroke = max(1, size // 16)
    neighbor_outside = False
    for dx in range(-stroke, stroke + 1):
        for dy in range(-stroke, stroke + 1):
            if not inside(x + dx, y + dy):
                neighbor_outside = True
                break
        if neighbor_outside:
            break
    if neighbor_outside:
        return (255, 255, 255, 255)

    core = max(2, size // 5)
    cx0 = (size - core) // 2
    if cx0 <= x < cx0 + core and cx0 <= y < cx0 + core:
        return (255, 255, 255, 255)
    return (10, 10, 10, 255)


def dib(size: int) -> bytes:
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        size,
        size * 2,
        1,
        32,
        0,
        size * size * 4,
        0,
        0,
        0,
        0,
    )
    rows = bytearray()
    for y in range(size - 1, -1, -1):
        for x in range(size):
            r, g, b, a = pixel(size, x, y)
            rows.extend((b, g, r, a))
    mask_row = ((size + 31) // 32) * 4
    mask = bytes(mask_row * size)
    return header + bytes(rows) + mask


def main() -> None:
    sizes = (16, 32, 48)
    images = [dib(size) for size in sizes]
    header = struct.pack("<HHH", 0, 1, len(sizes))
    offset = 6 + 16 * len(sizes)
    entries = bytearray()
    blobs = bytearray()
    for size, image in zip(sizes, images):
        entries.extend(
            struct.pack(
                "<BBBBHHII",
                size if size < 256 else 0,
                size if size < 256 else 0,
                0,
                0,
                1,
                32,
                len(image),
                offset,
            )
        )
        blobs.extend(image)
        offset += len(image)
    destination = Path(__file__).resolve().parents[1] / "assets" / "AppIcon.ico"
    destination.write_bytes(header + bytes(entries) + bytes(blobs))
    print(destination, destination.stat().st_size)


if __name__ == "__main__":
    main()
