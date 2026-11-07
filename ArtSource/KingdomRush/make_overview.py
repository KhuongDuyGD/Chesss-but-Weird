"""Compose actual Blender renders, preserving their physical relative heights."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import json
ROOT=Path(__file__).resolve().parents[2];SRC=Path(__file__).parent;OUT=ROOT/'Assets/Skins/KingdomRushCollab'
FONT=Path('C:/Windows/Fonts')
def font(size,bold=False):return ImageFont.truetype(str(FONT/('seguisb.ttf' if bold else 'segoeui.ttf')),size)
W,H=3000,2040
img=Image.new('RGB',(W,H),(13,19,28));d=ImageDraw.Draw(img)
gold=(224,181,85);muted=(147,161,178);white=(232,236,240)
d.text((78,42),'KINGDOM RUSH',font=font(66,True),fill=white)
d.text((80,123),'CHESS COLLAB   /   THE CROWN & THE DARK TOWER',font=font(23),fill=gold)
d.text((W-82,61),'12 EDITABLE 3D SCULPTS',font=font(25,True),anchor='ra',fill=white)
d.text((W-82,103),'BLENDER SOURCE  +  UNITY FBX',font=font(20),anchor='ra',fill=muted)
d.line((80,176,W-80,176),fill=(67,65,54),width=2)
manifest=json.loads((OUT/'model_manifest.json').read_text())
roles=['TỐT / PAWN','XE / ROOK','MÃ / KNIGHT','TƯỢNG / BISHOP','HẬU / QUEEN','VUA / KING']
for row,faction in enumerate(['Denas','Veznan']):
    top=232+row*861;bottom=top+789
    accent=(62,130,233) if row==0 else (172,75,211)
    fill=(23,34,49) if row==0 else (34,25,43)
    label='DENAS  /  LIÊN MINH LINIREA  /  QUÂN TRẮNG' if row==0 else 'VEZ’NAN  /  THẾ LỰC HẮC ÁM  /  QUÂN ĐEN'
    d.text((80,top-42),label,font=font(25,True),fill=accent)
    for col in range(6):
        x=80+col*476;right=x+456
        d.rounded_rectangle((x,top,right,bottom),radius=18,fill=fill,outline=(47,56,67),width=2)
        d.line((x+20,top+1,right-20,top+1),fill=accent,width=3)
        entry=next(p for p in manifest['models'] if p['name'].endswith('_'+faction) and p['name'].startswith(f'{col+1:02d}_'))
        art=Image.open(OUT/'Previews'/(entry['name']+'.png')).convert('RGBA');bb=art.getbbox();art=art.crop(bb)
        height=round(615*entry['original_type_height_m']/1.7)
        art=ImageOps.contain(art,(428,height),Image.Resampling.LANCZOS)
        img.paste(art,(x+(456-art.width)//2,top+665-art.height),art)
        d.text((x+228,top+696),roles[col],font=font(24,True),fill=white,anchor='mm')
        d.text((x+228,top+741),f"H {entry['dimensions_m'][2]:.3f} m   •   {round(entry['triangles']/1000,1):.1f}k tris",font=font(18),fill=muted,anchor='mm')
d.text((80,1991),'GIỮ TỶ LỆ ẢNH GỐC • THU ĐỀU CẢ BỘ VỪA Ô CỜ • PHẦN KHUẤT TỰ DỰNG • BẢN KIỂM TRA ART',font=font(22),fill=muted)
img.save(OUT/'Previews/KingdomRush_Pieces_Overview.jpg',quality=95,subsampling=0)
img.save(SRC/'Renders/KingdomRush_Pieces_Overview.jpg',quality=95,subsampling=0)
print('Saved overview',img.size)
