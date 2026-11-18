from pathlib import Path

from reportlab.pdfgen import canvas

destination = Path(__file__).with_name("ClickCircle_acceptance.pdf")
document = canvas.Canvas(str(destination), pagesize=(600, 420))
document.setTitle("ClickCircle acceptance")
for rotation in (0, 90, 180, 270):
    document.setPageRotation(rotation)
    document.setFont("Helvetica", 12)
    document.drawString(40, 380, f"ClickCircle acceptance - rotation {rotation}")
    document.setFont("Helvetica", 18)
    document.drawString(80, 330, "LI-282")
    document.drawString(80, 285, "LI-156x8rows")
    document.setFont("Helvetica", 12)
    for number, x in enumerate((310, 350, 390), 1):
        document.drawString(x - 3, 230, str(number))
        document.line(x - 4, 250, x + 4, 250)
        document.line(x, 246, x, 254)
    document.showPage()
document.save()
print(destination)
