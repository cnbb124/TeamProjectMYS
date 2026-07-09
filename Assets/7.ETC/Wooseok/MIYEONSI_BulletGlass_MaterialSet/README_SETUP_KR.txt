MIYEONSI Bullet Glass Material Set
=================================

목표 느낌
---------
니어 오토마타식 붉은 탄막을 참고해서 만든 '투명 에너지막 + Glossy 유리구슬 + 내부 발광 코어'용 Unity Built-in Render Pipeline 머터리얼 세트야.

구성 파일
---------
Textures/
- MIYEONSI_BulletGlass_AlbedoAlpha.png : 색 + 투명도
- MIYEONSI_BulletGlass_Normal.png : 유리 표면 굴곡 노말
- MIYEONSI_BulletGlass_Roughness.png : 거칠기. 어두울수록 더 매끈함
- MIYEONSI_BulletGlass_Smoothness.png : Unity용 Smoothness 참고맵
- MIYEONSI_BulletGlass_Height.png : 요철/에너지막 높이
- MIYEONSI_BulletGlass_Emission.png : 발광 라인
- MIYEONSI_BulletGlass_OpacityMask.png : 투명도 마스크
- MIYEONSI_BulletGlass_Mask_RGBA.png : R=Opacity, G=Smoothness, B=Height, A=Energy
- MIYEONSI_BulletGlass_FresnelRamp.png : 림/프레넬 색상 램프
- MIYEONSI_BulletGlass_RingSprite_RGBA.png : 외곽 글로우/파티클 링
- MIYEONSI_BulletGlass_CoreSprite_RGBA.png : 내부 코어/글로우 스프라이트

Shaders/
- MIYEONSI_EnergyBullet_GlossyGlass.shader : 외피 유리구슬용 투명 Glossy Shader
- MIYEONSI_EnergyBullet_AdditiveCore.shader : 내부 코어/글로우용 Additive Shader

Editor/
- MIYEONSI_CreateBulletGlassMaterials.cs : Unity 메뉴에서 자동으로 머터리얼 2개 생성

Unity 적용법
------------
1. 이 ZIP 안의 Assets 폴더를 네 Unity 프로젝트의 Assets 폴더에 그대로 넣어.
2. Unity 상단 메뉴에서 Tools > MIYEONSI > Create Bullet Glass Materials 실행.
3. 생성된 경로:
   Assets/MIYEONSI/BulletGlass/Materials/
   - MAT_EnergyBullet_Outer_GlossyGlass
   - MAT_EnergyBullet_Inner_AdditiveCore

프리팹 추천 구조
----------------
EnergyBullet_Prefab
- OuterSphere : Unity Sphere 또는 저폴리 UV Sphere
  - Scale: 1.0
  - Material: MAT_EnergyBullet_Outer_GlossyGlass
  - Cast Shadows: Off
  - Receive Shadows: Off
- InnerSphere 또는 Billboard Quad
  - Scale: 0.62 ~ 0.75
  - Material: MAT_EnergyBullet_Inner_AdditiveCore
- Optional Particle Ring
  - Texture: MIYEONSI_BulletGlass_RingSprite_RGBA
  - Blend: Additive
  - Size: 탄 크기의 1.1 ~ 1.35배

추천 세팅
---------
Outer Glass:
- Alpha: 0.28 ~ 0.42
- Smoothness: 0.90 ~ 0.98
- Fresnel Power: 1.8 ~ 2.6
- Fresnel Emission: 2.5 ~ 5.0
- Emission Intensity: 2.0 ~ 4.0
- Normal Strength: 0.45 ~ 0.80

탄막용 최적화 팁
----------------
- 탄 하나당 진짜 고폴리 구체를 쓰면 CPU/GPU가 짜증낼 수 있어. 16~24 segment UV sphere 정도면 충분해.
- 그림자 꺼. 탄막은 그림자보다 발광/실루엣이 더 중요함.
- 대량 탄막은 MaterialPropertyBlock으로 색만 바꿔. 머터리얼 복사본을 수백 개 만들면 씬이 금방 끈적해져.
- Built-in에서 Bloom을 쓰려면 Post Processing Stack을 붙이는 게 제일 편해.
- Bloom이 없으면 RingSprite와 CoreSprite를 Additive Particle로 같이 깔면 화면빨이 확 살아남.

Color Seed
----------
Reference-inspired palette:
- Base RGB: (np.int64(236), np.int64(52), np.int64(91))
- Hot RGB: (np.int64(255), np.int64(96), np.int64(149))
- Deep RGB: (np.int64(113), np.int64(24), np.int64(48))
