import cv2
import numpy as np
import os

base_dir = r"c:\Users\user\Desktop\Project2\Assets\SW\Sprites"
configs = {
    "chest.png": (5, 3, 14),
    "helmet.png": (4, 3, 11),
    "weapon.png": (3, 1, 3) # Let's assume weapon might be 3 cols, 1 row just in case? Or 1 col, 3 rows. If 1x3 is WxH, it's 1 col, 3 rows.
}

# Fix config based on standard WxH convention
configs["weapon.png"] = (1, 3, 3) 
configs["chest.png"] = (5, 3, 14)
configs["helmet.png"] = (4, 3, 11)

for file_name, (cols, rows, total) in configs.items():
    path = os.path.join(base_dir, file_name)
    if not os.path.exists(path):
        print(f"Not found: {path}")
        continue
        
    img = cv2.imread(path, cv2.IMREAD_COLOR)
    if img is None:
        continue
        
    h, w = img.shape[:2]
    
    cell_w = w // cols
    cell_h = h // rows
    
    prefix = os.path.splitext(file_name)[0]
    count = 1
    
    for row in range(rows):
        for col in range(cols):
            if count > total:
                break
                
            x1 = col * cell_w
            y1 = row * cell_h
            x2 = x1 + cell_w
            y2 = y1 + cell_h
            
            cell = img[y1:y2, x1:x2]
            
            # Convert to gray to mask background
            gray = cv2.cvtColor(cell, cv2.COLOR_BGR2GRAY)
            bg_color = gray[5, 5] # top-left slightly offset
            
            if bg_color < 20: # black
                mask = cv2.inRange(gray, 20, 255)
            elif bg_color > 235: # white
                mask = cv2.inRange(gray, 0, 235)
            else: # fallback
                mask = cv2.inRange(gray, 10, 255)
                
            # smooth the mask to remove noise
            kernel = np.ones((5,5), np.uint8)
            mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
            
            # Find bounds to crop the transparent padding
            contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            
            b, g, r = cv2.split(cell)
            rgba = cv2.merge((b, g, r, mask))
            
            if contours:
                min_x, min_y, max_x, max_y = cell_w, cell_h, 0, 0
                for c in contours:
                    cx, cy, cw, ch = cv2.boundingRect(c)
                    if cw < 5 or ch < 5: continue
                    if cx < min_x: min_x = cx
                    if cy < min_y: min_y = cy
                    if cx + cw > max_x: max_x = cx + cw
                    if cy + ch > max_y: max_y = cy + ch
                
                if max_x > min_x and max_y > min_y:
                    min_x = max(0, min_x - 10)
                    min_y = max(0, min_y - 10)
                    max_x = min(cell_w, max_x + 10)
                    max_y = min(cell_h, max_y + 10)
                    
                    rgba = rgba[min_y:max_y, min_x:max_x]
            
            out_path = os.path.join(base_dir, f"{prefix}_{count}.png")
            cv2.imwrite(out_path, rgba)
            print(f"Saved: {out_path} with size {rgba.shape}")
            
            count += 1
