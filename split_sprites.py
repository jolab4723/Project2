import cv2
import numpy as np
import os

base_dir = r"c:\Users\user\Desktop\Project2\Assets\SW\Sprites"
files_to_process = ["weapon.png", "chest.png", "helmet.png"]

for file_name in files_to_process:
    path = os.path.join(base_dir, file_name)
    if not os.path.exists(path):
        continue
    
    img = cv2.imread(path, cv2.IMREAD_COLOR)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    
    bg_color = gray[0, 0]
    if bg_color < 20:
        _, thresh = cv2.threshold(gray, 20, 255, cv2.THRESH_BINARY)
    elif bg_color > 235:
        _, thresh = cv2.threshold(gray, 235, 255, cv2.THRESH_BINARY_INV)
    else:
        _, thresh = cv2.threshold(gray, 10, 255, cv2.THRESH_BINARY)
        
    kernel = np.ones((15, 15), np.uint8)
    closed = cv2.morphologyEx(thresh, cv2.MORPH_CLOSE, kernel)
    
    contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    prefix = os.path.splitext(file_name)[0]
    count = 1
    
    height, width = gray.shape
    contours = sorted(contours, key=lambda c: (cv2.boundingRect(c)[1] // 30) * width + cv2.boundingRect(c)[0])
    
    for c in contours:
        x, y, w, h = cv2.boundingRect(c)
        if w < 30 or h < 30: 
            continue
            
        crop_bgr = img[y:y+h, x:x+w]
        
        # Smooth the mask to avoid harsh edges
        crop_mask = thresh[y:y+h, x:x+w]
        
        b, g, r = cv2.split(crop_bgr)
        crop_rgba = cv2.merge((b, g, r, crop_mask))
        
        out_name = f"{prefix}_{count}.png"
        out_path = os.path.join(base_dir, out_name)
        
        cv2.imwrite(out_path, crop_rgba)
        print(f"Saved: {out_name} ({w}x{h})")
        count += 1
