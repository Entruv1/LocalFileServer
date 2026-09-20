#!/usr/bin/env python3
"""生成本地文件服务器图标：蓝色渐变圆角方块 + 白色文件夹 + 下载箭头"""
from PIL import Image, ImageDraw, ImageFilter
import math

SIZE = 512  # 高分辨率绘制，最终缩到 256

def rounded_rect(draw, box, radius, fill):
    """绘制抗锯齿圆角矩形（4x 超采样）"""
    img = Image.new("RGBA", (SIZE*4, SIZE*4), (0,0,0,0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(
        [box[0]*4, box[1]*4, box[2]*4, box[3]*4],
        radius=radius*4, fill=fill)
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    draw.alpha_composite(img)
    return img

def vertical_gradient(size, top, bottom):
    """垂直渐变 RGBA"""
    img = Image.new("RGBA", (1, size))
    d = ImageDraw.Draw(img)
    for y in range(size):
        t = y / max(size-1, 1)
        col = tuple(int(top[i] + (bottom[i]-top[i])*t) for i in range(3)) + (255,)
        d.line([(0,y),(0,y)], fill=col)
    return img.resize((size, size), Image.LANCZOS)

def main():
    img = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))

    # 1) 渐变圆角背景（留 20px 边距）
    margin = 34
    bg_grad = vertical_gradient(SIZE, (0x2E,0x8F,0xE8), (0x14,0x5F,0xB0))
    # 用渐变填充圆角矩形
    bg = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    # 生成圆角 mask
    mask = Image.new("L", (SIZE, SIZE), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([margin, margin, SIZE-margin, SIZE-margin],
                         radius=int((SIZE-margin*2)*0.22), fill=255)
    bg.paste(bg_grad, (0,0), mask)
    img.alpha_composite(bg)

    # 2) 顶部高光（营造玻璃感）
    glow = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    gd = ImageDraw.Draw(glow)
    gd.rounded_rectangle([margin, margin, SIZE-margin, int(SIZE*0.72)],
                         radius=int((SIZE-margin*2)*0.22),
                         fill=(255,255,255,36))
    glow = glow.filter(ImageFilter.GaussianBlur(28))
    img.alpha_composite(glow)

    # 3) 底部柔和阴影
    shadow = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    sd = ImageDraw.Draw(shadow)
    sd.ellipse([margin+40, int(SIZE*0.80), SIZE-margin-40, int(SIZE*0.98)],
               fill=(0,0,0,40))
    shadow = shadow.filter(ImageFilter.GaussianBlur(22))
    img.alpha_composite(shadow)

    # 4) 白色文件夹（带标签页）
    fx0, fy0, fx1, fy1 = margin+58, margin+128, SIZE-margin-58, SIZE-margin-70
    # 主体圆角矩形
    fold = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    fd = ImageDraw.Draw(fold)
    fd.rounded_rectangle([fx0, fy0, fx1, fy1], radius=46, fill=(255,255,255,255))
    # 标签页（主体上方的突出折角）
    tab = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    td = ImageDraw.Draw(tab)
    td.rounded_rectangle([fx0+4, fy0-50, fx0+158, fy0+8], radius=22, fill=(255,255,255,255))
    fold.alpha_composite(tab)
    img.alpha_composite(fold)

    # 5) 下载箭头（蓝色，突出传输/抓取）— 用文件夹同色系深蓝，做内嵌镂空效果
    cx = SIZE//2
    arrow_top = fy0 + 66
    arrow_bot = fy1 - 60
    shaft_w = 34
    # 箭杆
    shaft = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    sd2 = ImageDraw.Draw(shaft)
    sd2.rounded_rectangle([cx-shaft_w//2, arrow_top, cx+shaft_w//2, arrow_bot],
                          radius=shaft_w//2, fill=(0x14,0x5F,0xB0,255))
    # 箭头头
    head = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    hd = ImageDraw.Draw(head)
    half = int(shaft_w*1.25)
    hd.polygon([(cx-half, arrow_bot-58), (cx+half, arrow_bot-58),
                (cx, arrow_bot+52)], fill=(0x14,0x5F,0xB0,255))
    shaft.alpha_composite(head)
    img.alpha_composite(shaft)

    return img

img = main()
# 生成 256 和 512 两种规格
img256 = img.resize((256,256), Image.LANCZOS)
img512 = img.resize((512,512), Image.LANCZOS)
img256.save("app_icon.png")
# 生成多分辨率 ICO
img256.save("app_icon.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print("图标已生成: app_icon.png, app_icon.ico")
