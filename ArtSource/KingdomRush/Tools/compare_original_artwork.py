"""Measure calibrated-view evidence. These checks do not certify every 3D angle."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,sys
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];REF=ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab'
names=sys.argv[1:] or [p.stem for p in sorted(REF.glob('0*.png'))]
out=SRC/'Reconstructions/Comparisons';out.mkdir(exist_ok=True);reports=[]
for name in names:
    original=Image.open(REF/(name+'.png')).convert('RGB');model=Image.open(ROOT/'Assets/Skins/KingdomRushCollab/Previews'/(name+'.png')).convert('RGBA').resize(original.size,Image.Resampling.LANCZOS)
    flat=Image.new('RGB',model.size,'white');flat.paste(model,mask=model.getchannel('A'))
    mask=np.array(Image.open(SRC/'Reconstructions/Masks'/(name+'_mask.png')))>127;alpha=np.array(model.getchannel('A'))>127
    mae=np.abs(np.array(flat).astype(float)-np.array(original).astype(float))[mask].mean();iou=(mask&alpha).sum()/(mask|alpha).sum()
    reports.append({'name':name,'calibrated_view_silhouette_iou':float(iou),'foreground_RGB_mean_absolute_error_0_255':float(mae),'pixel_identical':bool(np.array_equal(np.array(flat),np.array(original))),'all_angles_certified':False})
    board=Image.new('RGB',(1200,650),(237,239,242));draw=ImageDraw.Draw(board)
    for x,image,title in [(0,original,'ORIGINAL IMAGE'),(600,flat,'3D RENDER - CALIBRATED VIEW')]:
        image.thumbnail((590,590),Image.Resampling.LANCZOS);board.paste(image,(x+(600-image.width)//2,35+(590-image.height)//2));draw.text((x+20,12),title,fill=(25,29,38))
    draw.text((20,626),name+' | '+f'Contour overlap {iou:.4f}; RGB error {mae:.2f}/255. Other angles are inferred.',fill=(25,29,38))
    board.save(out/(name+'_Compare.jpg'),quality=94)
(out/'calibrated_view_report.json').write_text(json.dumps(reports,indent=2))
print(json.dumps(reports,indent=2))
