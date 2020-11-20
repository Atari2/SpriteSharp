;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; A small shuriken
;; This projectile moves straight right/left
;; and when it entounters wall, it'll start falling down like SMB2 Snifit's projectile.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Disappear on power loss
;If option is enabled, projectile will disappear if player have lost power.
;Freeram should be the same as in uberasm!
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!DisappearOnPowerLoss = 0
!PowerValue = $02		;which power allows shooting shuriken? use value from power-up's cfg property, it's calculated automatically	
!FreeRam = $0DC4|!Base2

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Weakness check:
;Check for config setting.
;It'll check for following weaknesses if enabled.
;If not it'll kill regardless if protection is enabled or not.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!FireKill = 0
!CapeKill = 1
!SlideKill = 1
!OtherKill = 0

!166EBits = 0			;don't edit this one

if !FireKill
  !166EBits := !166EBits|$10
endif

if !CapeKill
  !166EBits := !166EBits|$20
endif

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;"Used" speed
;X-Speed set when hitting walls or enemies, opposite of which direction they were moving
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!UsedShurikenXSpdRight = $10
!UsedShurikenXSpdLeft = $F0

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Death type:
;0 - make sprite fall down
;1 and high - disappear in puff of smoke
;option 0 may not work 
;if sprite already has "disappear in puff of smoke" option enabled in CFG file
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!DeathType = 1

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Sound effects stuff
;https://www.smwcentral.net/?p=viewthread&t=6665 for more info
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!KillSound = $03

!KillSoundBank = $1DF9|!Base2

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Score
;view ROM address $02ACE5 in ROM map.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!DoGiveScore = 1		;Does it gives you score?

!Score = $01			;100 by default

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Tables
;First table is "Killed Sprite's X speed", which is used when !DeathType = 0
;Other two are GFX related.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

KillXSpeed:
	db $F0,$10

ShurikenTiles:
	db $EF,$84,$EF,$84

ShurikenProps:
	db $02,$03,$C2,$C3

SmokeDeath:
LDA #$01
STA $170B|!Base2,x		;Turn into extended smoke

LDA #$0F			;
STA $176F|!Base2,x		;Set animation timer
RTL

Print "MAIN ",pc
Shuriken:
LDA $9D				;don't do things
BNE GFX				;

If !DisappearOnPowerLoss
  LDA !FreeRam
  CMP #!PowerValue+1
  BNE SmokeDeath
endif

LDA $175B|!Base2,x		;
BEQ .SurfaceNotHit		;

LDA #$00			;
%ExtendedSpeed()		;
BRA GFX				;

.SurfaceNotHit
;%ExtendedBlockInteraction()	;i'd use this, but it's kinda not good.
JSR ObjInteraction		;explained in this routine
BCC .no				;

JSR ShurikenHit			;trigger hit code

INC $1DF9|!Base2		;play "hit head" sound

.no
LDA #$02			;
%ExtendedSpeed()		;

STZ $173D|!Base2,x		;

JSR SpriteInteract		;interact with sprites

INC $1765|!Base2,x		;frame counter

GFX:
%ExtendedGetDrawInfo()		;

LDA $01				;
STA $0200|!Base2,y		;

LDA $02				;
STA $0201|!Base2,y		;

LDA $1779|!Base2,x		;save "behind layers" info
STA $01				;

LDA $1765|!Base2,x		;
LSR #2				;
AND #$03			;
TAX				;
LDA ShurikenTiles,x		;tiles, tiles, of course!
STA $0202|!Base2,y		;

LDA ShurikenProps,x		;
EOR $00				;
ORA $64				;
STA $0203|!Base2,y		;

LDX $01				;
BEQ .NoFlips			;
AND #$CF			;
ORA #$10			;
STA $0203|!Base2,y		;Do various flips to create an illusion of rolling

.NoFlips
TYA				;
LSR #2				;
TAY				;
LDA #$00			;8x8 tile
STA $0420|!Base2,y		;
LDX $15E9|!Base2		;
RTL				;

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Interact with other sprites
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SpriteInteract:
LDY #!SprSize-1			;go through loop

Loopityloop:
LDA !14C8,y			;if sprite's in normal state
CMP #$08			;
BCC .Not			;don't bother them

if !OtherKill
  LDA !167A,y			;if sprite cant interact with stuff like fire
  AND #$02			;
  ORA !1632,y			;Or if sprite's behind layers
else
  LDA !1632,y			;
endif
EOR $1779|!Base2,x		;Unless projectile is also behind layers
BNE .Not			;

PHX				;
TYX				;
JSL $03B69F|!BankB		;get sprite's clipping
PLX				;
JSR GetExClipping		;Get Extended Sprite's Clipping
JSL $03B72B|!BankB		;interact
BCC .Not			;if no interaction, no interaction, next please

If !166EBits
  LDA !166E,y
  AND #!166EBits
  BNE .ShirkenHit
endif

if !SlideKill
  LDA !190F,y
  AND #$04
  BNE .ShirkenHit
endif

If !DeathType			;check if it should die in smoke pain...
  LDA !1656,y			;
  ORA #$80			;
  STA !1656,y			;
else				;...or just in pain.
  LDA #$D0			;Upward speed
  STA !AA,y			;

  LDA !157C,y
  TAX
  LDA KillXSpeed,x		;X speed depending on it's facing
  STA !B6,y			;

  TYX				;do transfer magic.
  %SubHorzPos()			;Make dead sprite face mario?
  TYA				;
  TXY				;
  EOR #$01			;Flip it's direction, so it doesn't actually face mario.
  STA !157C,y			;

  LDX $15E9|!Base2		;
endif

LDA #$02			;make sprite RIP
STA !14C8,y			;

LDA #!KillSound			;under some sound effect
STA !KillSoundBank		;

If !DoGiveScore			;if the result should be rewarding
  TYX
  if !SA1 == 1
    REP #$20
    TXA
    CLC : ADC #!sprite_y_low
    STA $CC
    TXA
    CLC : ADC #!sprite_x_low
    STA $EE
    SEP #$20
  endif
  LDA #!Score			;here's your score! Not sure if it's usefull though
  JSL $02ACE5|!BankB		;
  TXY
  LDX $15E9|!Base2
endif				;

.ShirkenHit
JMP ShurikenHit
;RTS				;

.Not
DEY				;
BPL Loopityloop			;
RTS				;

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;
;Hit Box Thingy
;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

GetExClipping:
LDA $171F|!Base2,x		;Get X position
SEC				;Calculate hitbox
SBC #$02			;
STA $00				;

LDA $1733|!Base2,x		;
SBC #$00			;Take care of high byte
STA $08				;

LDA #$0C			;length
STA $02				;

LDA $1715|!Base2,x		;Y pos
SEC				;
SBC #$04			;
STA $01				;

LDA $1729|!Base2,x		;
SBC #$00			;
STA $09				;

LDA #$13			;length
STA $03				;
RTS				;

ShurikenHit:
INC $175B|!Base2,x		;

LDA #!UsedShurikenXSpdRight	;change it's x-speed on wall hit depending on it's "facing"
LDY $1747|!Base2,x 		;there's no actual facing for sprites, so we use speed as indicator
BMI .NotDifSpd			;

LDA #!UsedShurikenXSpdLeft	;

.NotDifSpd
STA $1747|!Base2,x		;
RTS				;

ObjInteraction:
PHK				;you may ask me "Hey Russ, Why don't you use %ExtendedBlockInteraction()?"
PEA.w .Re-1			;well, my dear friend, I noticed some bad collision detection it provides compared to SMW's, and it bugs me.
PEA.w $02A772|!BankB-1		;I'm kinda lazy to investigate on why it doesn't works right
JML $02A56E|!BankB		;so I'll use this method for now, untill that subroutine is fixed.

.Re
RTS				;