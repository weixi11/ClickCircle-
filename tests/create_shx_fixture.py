"""Create vector-only drawing labels with AutoCAD-style SHX original annotations."""
from io import BytesIO
from pathlib import Path

from fontTools.pens.reportLabPen import ReportLabPen
from fontTools.ttLib import TTFont
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ArrayObject, DictionaryObject, FloatObject, NameObject, NumberObject, TextStringObject
from reportlab.pdfgen import canvas


destination = Path(__file__).with_name("ClickCircle_shx_selection.pdf")
font = TTFont(r"C:\Windows\Fonts\arial.ttf")
glyphs = font.getGlyphSet()
cmap = font.getBestCmap()
units = font["head"].unitsPerEm
buffer = BytesIO()
document = canvas.Canvas(buffer, pagesize=(600, 420))
document.setTitle("ClickCircle SHX selection")
page_annotations = []


def outlines(text, x, y, size=18):
    document.saveState()
    document.translate(x, y)
    scale = size / units
    document.scale(scale, scale)
    advance = 0
    for char in text:
        name = cmap[ord(char)]
        path = document.beginPath()
        path.closePath = path.close
        pen = ReportLabPen(glyphs, path)
        glyphs[name].draw(pen)
        document.drawPath(pen.path, fill=1, stroke=0)
        width = font["hmtx"][name][0]
        document.translate(width, 0)
        advance += width
    document.restoreState()
    return [x, y - size * 0.22, x + advance * scale, y + size * 0.8]


for rotation in (0, 90, 180, 270):
    document.setPageRotation(rotation)
    document.setFont("Helvetica", 12)
    document.drawString(35, 385, f"SHX selection - rotation {rotation}")
    annots = []
    for index, (text, x, y) in enumerate((
        ("LI-282", 80, 235),
        ("LI-058", 80, 325),
        ("LI-058", 80, 280),
        ("3x2500/5A MCT", 300, 280),
    )):
        rect = outlines(text, x, y)
        if index == 1:
            rect = [rect[2], rect[3], rect[0], rect[1]]
        annots.append(("AutoCAD SHX Text", text, rect))
    document.setFont("Helvetica", 18)
    document.drawString(300, 325, "NATIVE")
    annots.append(("AutoCAD SHX Text", "NATIVE", [300, 321, 366, 339.4]))
    outlines("COMMENT", 300, 235)
    annots.append(("Engineer", "IGNORE-COMMENT", [300, 230, 405, 250]))
    annots.append(("AutoCAD SHX Text", " \t ", [80, 180, 140, 200]))
    document.setFont("Helvetica", 12)
    for index, x in enumerate((350, 400, 450), 1):
        document.line(x - 5, 135, x + 5, 135)
        document.line(x, 130, x, 140)
        document.drawString(x - 3, 110, str(index))
    page_annotations.append(annots)
    document.showPage()
document.save()

writer = PdfWriter()
for source, annotations in zip(PdfReader(buffer).pages, page_annotations):
    page = writer.add_page(source)
    refs = ArrayObject()
    for title, text, rect in annotations:
        annotation = DictionaryObject({
            NameObject("/Type"): NameObject("/Annot"),
            NameObject("/Subtype"): NameObject("/Square"),
            NameObject("/T"): TextStringObject(title),
            NameObject("/Contents"): TextStringObject(text),
            NameObject("/Rect"): ArrayObject([FloatObject(value) for value in rect]),
            NameObject("/F"): NumberObject(64),
            NameObject("/CA"): FloatObject(0),
            NameObject("/Border"): ArrayObject([NumberObject(0)] * 3),
        })
        refs.append(writer._add_object(annotation))
    page[NameObject("/Annots")] = refs
writer.add_metadata({"/Title": "ClickCircle SHX selection"})
with destination.open("wb") as stream:
    writer.write(stream)
print(destination)
