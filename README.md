# BalloonPdf

BalloonPdf is a desktop tool for detecting drawing dimensions, adding balloon annotations to PDFs, and exporting the detected dimensions for review.

## OCR language data setup

Image-based OCR uses Tesseract English language data. Before running OCR, download the official `eng.traineddata` file:

- [Tesseract English language data (`eng.traineddata`)](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata)

Then use one of these setup options:

1. Create a `tessdata` directory beside the built or published app executable and place `eng.traineddata` in that directory.
2. Set `TESSDATA_PREFIX` to the directory that contains `eng.traineddata`.

`TESSDATA_PREFIX` should point directly at the tessdata directory that contains the file, not at that directory's parent. If OCR cannot find `eng.traineddata`, BalloonPdf reports the exact directories it checked so you can correct the setup.
