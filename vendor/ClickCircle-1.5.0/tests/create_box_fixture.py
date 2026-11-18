from pathlib import Path

from reportlab.pdfgen import canvas


destination = Path(__file__).with_name("ClickCircle_box_selection.pdf")
document = canvas.Canvas(str(destination), pagesize=(600, 420))
document.setTitle("ClickCircle box selection")
for rotation in (0, 90, 180, 270):
    document.setPageRotation(rotation)
    document.setFont("Helvetica", 12)
    document.drawString(35, 385, f"Box selection - rotation {rotation}")
    document.setFont("Helvetica", 18)
    # Drawing order deliberately differs from visual reading order.
    document.drawString(80, 235, "LI-282")
    document.drawString(80, 325, "LI-058")
    document.drawString(80, 280, "LI-058")
    document.drawString(300, 325, "LI-156x8rows")
    document.drawString(300, 235, "KEEP-OUT")
    document.setFont("Helvetica", 12)
    for index, x in enumerate((350, 400, 450), 1):
        document.line(x - 5, 135, x + 5, 135)
        document.line(x, 130, x, 140)
        document.drawString(x - 3, 110, str(index))
    document.showPage()
document.save()
print(destination)
