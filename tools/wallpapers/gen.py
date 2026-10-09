# WinWeaver — WeavOS wallpaper generator
# Copyright (C) 2026 fel7xxx
# SPDX-License-Identifier: GPL-3.0-or-later
#
# Renders app/walls/wall1..4.jpg (3840x2160) and their 480x270 thumbnails.
# Needs Python 3 with numpy and Pillow, and the Manrope variable font (SIL Open Font License,
# https://fonts.google.com/specimen/Manrope) saved next to this script as Manrope.ttf.
import numpy as np, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
W,H=3840,2160
yy,xx=np.mgrid[0:H,0:W].astype(np.float32)
u=xx/W; v=yy/H
ICE=np.array([132,204,242],np.float32)
def base(c0,c1):
    # vertical-ish gradient with vignette
    t=np.clip(0.55*v+0.45*u,0,1)[...,None]
    img=np.array(c0,np.float32)*(1-t)+np.array(c1,np.float32)*t
    d=np.sqrt(((u-0.5)*1.6)**2+((v-0.5)*1.2)**2)
    img*= (1-0.55*np.clip(d,0,1)**2)[...,None]
    return img
def glow(cx,cy,r,strength,col=ICE):
    d=np.sqrt(((u-cx)*W/H)**2+(v-cy)**2)
    return (np.exp(-(d/r)**2)*strength)[...,None]*(col/255.0)
def ribbons(n,amp,freq,phase,thick,spread,center,strength,col=ICE,tilt=0.0):
    acc=np.zeros((H,W),np.float32)
    for i in range(n):
        k=i/(n-1) if n>1 else 0
        y0=center+(k-0.5)*spread
        y=y0+amp*np.sin(2*np.pi*(freq*u+phase+k*0.35))+0.4*amp*np.sin(2*np.pi*(2.3*freq*u+phase*1.7+k))+tilt*(u-0.5)
        dist=np.abs(v-y)
        acc+=np.exp(-(dist/thick)**2)*(0.35+0.65*np.sin(np.pi*k)**2)
    acc=acc/acc.max()
    return (acc*strength)[...,None]*(col/255.0)
def lines(n,amp,freq,thick,strength,col):
    acc=np.zeros((H,W),np.float32)
    for i in range(n):
        k=i/(n-1)
        y=0.12+k*0.8+amp*np.sin(2*np.pi*(freq*u+k*0.9))*np.sin(np.pi*u)+0.05*np.sin(2*np.pi*(0.6*u+k*2.1))
        acc+=np.exp(-((v-y)/thick)**2)
    acc=np.clip(acc,0,1.5)
    fade=np.exp(-(((u-0.5)*1.4)**2+((v-0.55)*1.6)**2)*1.2)
    return (acc*fade*strength)[...,None]*(col/255.0)
def weave(strength):
    a=np.sin(2*np.pi*(u*W+v*H)/46.0); b=np.sin(2*np.pi*(u*W-v*H)/46.0)
    over=(a>0)^(b>0)
    th=np.maximum(np.abs(a),np.abs(b))
    pat=np.where(over,np.abs(a),np.abs(b))**6
    fade=np.exp(-(((u-0.5)*1.3)**2+((v-0.5)*1.5)**2)*2.2)
    return (pat*fade*strength)[...,None]*(np.array([170,190,205],np.float32)/255.0)
def noise(img,s=2.2):
    rng=np.random.default_rng(7)
    return img+rng.normal(0,s,(H,W,1)).astype(np.float32)
def finish(img,name,textalpha=0.80):
    img=np.clip(noise(img),0,255).astype(np.uint8)
    im=Image.fromarray(img,'RGB').convert('RGBA')
    # textless copy for the program's home banner
    im.convert('RGB').resize((1600,900),Image.LANCZOS).save(name+'_h.jpg',quality=86,optimize=True,progressive=True)
    font=ImageFont.truetype('Manrope.ttf',300)
    try: font.set_variation_by_name('Medium')
    except Exception as e: print('var',e)
    layer=Image.new('RGBA',(W,H),(0,0,0,0))
    d=ImageDraw.Draw(layer)
    txt='WeavOS'
    bb=d.textbbox((0,0),txt,font=font)
    tw,th=bb[2]-bb[0],bb[3]-bb[1]
    x=(W-tw)//2-bb[0]; y=(H-th)//2-bb[1]
    # glow
    g=Image.new('RGBA',(W,H),(0,0,0,0)); gd=ImageDraw.Draw(g)
    gd.text((x,y),txt,font=font,fill=(132,204,242,255))
    g1=g.filter(ImageFilter.GaussianBlur(90)); g2=g.filter(ImageFilter.GaussianBlur(26))
    im=Image.alpha_composite(im,g1); im=Image.alpha_composite(im,g1); im=Image.alpha_composite(im,g2)
    d.text((x,y),txt,font=font,fill=(236,244,250,int(255*textalpha)))
    im=Image.alpha_composite(im,layer).convert('RGB')
    im.save(name+'.jpg',quality=90,optimize=True,progressive=True)
    im.resize((480,270),Image.LANCZOS).save(name+'_t.jpg',quality=88)
which=sys.argv[1]
if which=='1':  # Ice Silk
    img=base((16,18,22),(30,34,42))+glow(0.5,0.5,0.55,40)
    img+=ribbons(9,0.07,0.8,0.1,0.010,0.22,0.58,215,tilt=-0.08)
    img+=ribbons(5,0.05,0.6,0.55,0.035,0.18,0.62,40,col=np.array([90,140,200],np.float32))
    finish(img,'wall1')
elif which=='2':  # Graphite Weave
    img=base((20,21,24),(34,36,40))+weave(26)+glow(0.5,0.5,0.35,55)
    finish(img,'wall2')
elif which=='3':  # Aurora Frost
    img=base((10,13,18),(20,26,34))
    img+=ribbons(6,0.10,0.5,0.3,0.06,0.25,0.35,95,col=np.array([100,210,230],np.float32),tilt=0.15)
    img+=ribbons(4,0.08,0.7,0.8,0.03,0.15,0.30,70,col=np.array([140,170,250],np.float32),tilt=0.2)
    img+=glow(0.5,1.05,0.5,35)
    finish(img,'wall3')
elif which=='4':  # Silver Lines
    img=base((22,23,26),(14,15,18))+glow(0.5,0.52,0.45,30)
    img+=lines(70,0.09,0.9,0.0016,85,np.array([180,205,225],np.float32))
    finish(img,'wall4')
