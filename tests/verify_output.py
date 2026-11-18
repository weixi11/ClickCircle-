"""Check saved editable Circle annotations; emit a JSON verification report."""

import argparse
import json
import math
import sys
from collections import Counter
from pathlib import Path

from pypdf import PdfReader


def close(actual, expected, tolerance=0.00001):
    return math.isfinite(actual) and abs(actual - expected) <= tolerance


def color_matches(actual, rgb):
    return (
        actual is not None
        and len(actual) == 3
        and all(close(float(value), channel / 255) for value, channel in zip(actual, rgb))
    )


def has_appearance(annotation):
    appearance = annotation.get("/AP")
    if appearance is None:
        return False
    normal = appearance.get_object().get("/N")
    if normal is None:
        return False
    normal = normal.get_object()
    if hasattr(normal, "get_data"):
        return len(normal.get_data()) > 0
    return any(
        hasattr(value.get_object(), "get_data") and len(value.get_object().get_data()) > 0
        for value in normal.values()
    )


def size_pair(values):
    width, height = (float(value) for value in values)
    if not all(math.isfinite(value) and value > 0 for value in (width, height)):
        raise ValueError("Expected dimensions must be positive finite numbers.")
    return width, height


def verify(arguments):
    default_sizes = [(1.5, 1.2)] + [size_pair(values) for values in arguments.allow_size]
    subject_sizes = {}
    for subject, width, height in arguments.subject_size:
        subject_sizes.setdefault(subject, []).append(size_pair((width, height)))

    document = PdfReader(arguments.path)
    errors = []
    page_reports = []
    subjects = Counter()
    names = set()
    circle_count = 0
    for page_number, page in enumerate(document.pages, 1):
        rotation = (int(page.get("/Rotate", 0)) + arguments.view_rotation) % 360
        if rotation % 90:
            errors.append("page %d: unsupported display rotation %d" % (page_number, rotation))
        user_unit = float(page.get("/UserUnit", 1))
        circles = []
        for reference in page.get("/Annots", []):
            annotation = reference.get_object()
            if annotation.get("/Subtype") != "/Circle":
                continue
            circle_count += 1
            prefix = "page %d circle %d" % (page_number, len(circles) + 1)
            subject = str(annotation.get("/Subj", ""))
            subjects[subject] += 1
            rectangle = [float(value) for value in annotation["/Rect"]]
            width_mm = (rectangle[2] - rectangle[0]) * user_unit * 25.4 / 72
            height_mm = (rectangle[3] - rectangle[1]) * user_unit * 25.4 / 72
            if rotation in (90, 270):
                width_mm, height_mm = height_mm, width_mm
            allowed_sizes = subject_sizes.get(subject, default_sizes)
            if not any(
                close(width_mm, expected_width, 0.002) and close(height_mm, expected_height, 0.002)
                for expected_width, expected_height in allowed_sizes
            ):
                errors.append(
                    "%s: display size %.6fx%.6f mm is outside %s"
                    % (prefix, width_mm, height_mm, allowed_sizes)
                )
            opacity = float(annotation.get("/CA", 1))
            border = annotation.get("/BS")
            border = border.get_object() if border is not None else {}
            legacy_border = annotation.get("/Border", [0, 0, 1])
            border_width = float(border.get("/W", legacy_border[2]))
            stroke = annotation.get("/C")
            fill = annotation.get("/IC")
            flags = int(annotation.get("/F", 0))
            if not subject.strip():
                errors.append(prefix + ": missing subject")
            if annotation.get("/Contents", ""):
                errors.append(prefix + ": unexpected annotation contents")
            if not close(opacity, 0.6):
                errors.append(prefix + ": opacity is not 0.6")
            if not close(border_width, 0.2):
                errors.append(prefix + ": border width is not 0.2 pt")
            if border.get("/S", "/S") != "/S" or any(float(value) for value in border.get("/D", [])):
                errors.append(prefix + ": border is not solid")
            if len(legacy_border) > 3 and any(float(value) for value in legacy_border[3]):
                errors.append(prefix + ": legacy border contains a dash pattern")
            if not color_matches(stroke, (211, 47, 47)):
                errors.append(prefix + ": wrong stroke color")
            if not color_matches(fill, (239, 83, 80)):
                errors.append(prefix + ": wrong fill color")
            # Invisible, Hidden, NoView, ReadOnly, Locked, and LockedContents.
            if flags & (1 | 2 | 32 | 64 | 128 | 512):
                errors.append(prefix + ": annotation is hidden or locked")
            if not flags & 4:
                errors.append(prefix + ": annotation is not printable")
            if flags & (8 | 16):
                errors.append(prefix + ": unexpected NoZoom or NoRotate flag")
            if not has_appearance(annotation):
                errors.append(prefix + ": missing or empty normal appearance")
            name = str(annotation.get("/NM", ""))
            if not name or name in names:
                errors.append(prefix + ": annotation name is missing or duplicated")
            names.add(name)
            circles.append(
                {
                    "subject": subject,
                    "display_size_mm": [round(width_mm, 6), round(height_mm, 6)],
                    "native_rect_pt": rectangle,
                    "center_pdf_pt": [
                        (rectangle[0] + rectangle[2]) / 2,
                        (rectangle[1] + rectangle[3]) / 2,
                    ],
                    "opacity": opacity,
                    "border_width_pt": border_width,
                    "stroke_rgb": [round(float(value) * 255) for value in stroke or []],
                    "fill_rgb": [round(float(value) * 255) for value in fill or []],
                    "flags": flags,
                }
            )
        page_reports.append(
            {"page": page_number, "display_rotation": rotation, "circle_count": len(circles), "circles": circles}
        )

    if not circle_count:
        errors.append("No Circle annotations were found.")
    if arguments.expect_count is not None and circle_count != arguments.expect_count:
        errors.append("Expected %d circles; found %d." % (arguments.expect_count, circle_count))
    if arguments.expect_page_counts is not None:
        actual_counts = [page["circle_count"] for page in page_reports]
        if actual_counts != arguments.expect_page_counts:
            errors.append("Expected page counts %s; found %s." % (arguments.expect_page_counts, actual_counts))
    for subject, expected_count in arguments.expect_subject:
        if subjects[subject] != int(expected_count):
            errors.append(
                "Expected subject %r count %s; found %d." % (subject, expected_count, subjects[subject])
            )
    return {
        "ok": not errors,
        "path": str(Path(arguments.path).resolve()),
        "page_count": len(document.pages),
        "circle_count": circle_count,
        "subjects": dict(subjects),
        "pages": page_reports,
        "errors": errors,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", help="Saved PDF to inspect; never modified.")
    parser.add_argument("--allow-size", nargs=2, action="append", default=[], metavar=("WIDTH_MM", "HEIGHT_MM"),
                        help="Allow another display size in addition to 1.5 x 1.2 mm; repeatable.")
    parser.add_argument("--subject-size", nargs=3, action="append", default=[], metavar=("SUBJECT", "WIDTH_MM", "HEIGHT_MM"),
                        help="Override allowed sizes for one subject; repeatable for multiple sizes.")
    parser.add_argument("--view-rotation", type=int, default=0,
                        help="Additional editor-only display rotation, if used for all tested pages.")
    parser.add_argument("--expect-count", type=int)
    parser.add_argument("--expect-page-counts", type=int, nargs="+")
    parser.add_argument("--expect-subject", nargs=2, action="append", default=[], metavar=("SUBJECT", "COUNT"))
    arguments = parser.parse_args()
    try:
        report = verify(arguments)
    except Exception as exception:
        report = {"ok": False, "path": str(Path(arguments.path).resolve()), "errors": [str(exception)]}
    print(json.dumps(report, ensure_ascii=True, indent=2))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
