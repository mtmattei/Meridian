"""
Generate LinkedIn carousel PDF from HTML slides.
Screenshots each 1080x1350 slide and combines into a single PDF.
"""
import asyncio
from pathlib import Path
from playwright.async_api import async_playwright

HTML_FILE = Path(__file__).parent / "fukasawa-carousel-linkedin.html"
OUTPUT_PDF = Path(__file__).parent / "fukasawa-carousel-linkedin.pdf"

SLIDE_W = 1080
SLIDE_H = 1350
NUM_SLIDES = 15


async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await browser.new_page(viewport={"width": SLIDE_W, "height": SLIDE_H})

        file_url = HTML_FILE.as_uri()
        await page.goto(file_url, wait_until="networkidle")

        # Wait for fonts to load
        await page.wait_for_timeout(2000)

        screenshots = []
        for i in range(NUM_SLIDES):
            # Scroll to each slide
            await page.evaluate(f"window.scrollTo(0, {i * SLIDE_H})")
            await page.wait_for_timeout(400)

            # Screenshot just the visible viewport
            shot = await page.screenshot(type="png")
            screenshots.append(shot)
            print(f"  Captured slide {i + 1}/{NUM_SLIDES}")

        await browser.close()

    # Combine screenshots into PDF using Pillow
    from PIL import Image
    import io

    images = []
    for shot in screenshots:
        img = Image.open(io.BytesIO(shot)).convert("RGB")
        images.append(img)

    # Save as multi-page PDF
    images[0].save(
        str(OUTPUT_PDF),
        save_all=True,
        append_images=images[1:],
        resolution=144,  # High DPI for crisp LinkedIn rendering
    )

    print(f"\n  PDF saved: {OUTPUT_PDF}")
    print(f"  {len(images)} slides, {SLIDE_W}x{SLIDE_H}px each")


if __name__ == "__main__":
    asyncio.run(main())
