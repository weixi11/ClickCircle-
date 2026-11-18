"""Build a raster-only four-rotation fixture without a PDF text layer."""
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from pypdf import PdfReader, PdfWriter
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas


def main():
    root = Path(__file__).resolve().parent
    image = Image.new("RGB", (1000, 460), "white")
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 36)
    for text, x, y in [("LI-058", 60, 55), ("LI-058", 400, 55), ("LI-282", 60, 190)]:
        draw.text((x, y), text, font=font, fill="black")
    buffer = BytesIO()
    pdf = canvas.Canvas(buffer, pagesize=(720, 480), pageCompression=1)
    for _ in range(4):
        pdf.drawImage(ImageReader(image), 50, 180, width=500, height=230)
        pdf.showPage()
    pdf.save()
    reader = PdfReader(BytesIO(buffer.getvalue()))
    writer = PdfWriter()
    for index, page in enumerate(reader.pages):
        if index:
            page.rotate(index * 90)
        assert not page.extract_text()
        writer.add_page(page)
    target = root / "ClickCircle_ocr_selection.pdf"
    with target.open("xb") as output:
        writer.write(output)
    print(target)


if __name__ == "__main__":
    main()
