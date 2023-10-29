import cv2 as cv
BGRImage = cv.imread("D:\\Proj\\vs\\ParallelPictureProcessing\\scr\\123.bmp")
YCrCbImage = cv.cvtColor(BGRImage, cv.COLOR_BGR2YCR_CB)
cv.imshow('bef',BGRImage)
cv.imshow('af',YCrCbImage)

Y, Cr, Cb = cv2.split(imgYCrCB)

# Fill Y and Cb with 128 (Y level is middle gray, and Cb is "neutralized").
onlyCr = imgYCrCB.copy()
onlyCr[:, :, 0] = 128
onlyCr[:, :, 2] = 128
onlyCr_as_bgr = cv2.cvtColor(onlyCr, cv2.COLOR_YCrCb2BGR)  # Convert to BGR - used for display as false color

# Fill Y and Cr with 128 (Y level is middle gray, and Cr is "neutralized").
onlyCb = imgYCrCB.copy()
onlyCb[:, :, 0] = 128
onlyCb[:, :, 1] = 128
onlyCb_as_bgr = cv2.cvtColor(onlyCb, cv2.COLOR_YCrCb2BGR)  # Convert to BGR - used for display as false color


cv2.imshow('img', img)
cv2.imshow('Y', Y)
cv2.imshow('onlyCb_as_bgr', onlyCb_as_bgr)
cv2.imshow('onlyCr_as_bgr', onlyCr_as_bgr)
cv2.waitKey()
cv2.destroyAllWindows()

cv2.imwrite('Y.png', Y)
cv2.imwrite('onlyCb_as_bgr.png', onlyCb_as_bgr)
cv2.imwrite('onlyCr_as_bgr.png', onlyCr_as_bgr)
