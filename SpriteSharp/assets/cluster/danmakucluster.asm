;------------;
; Danmaku    ;
;------------;

; Initial XY position and direction are set by the generating sprite.

X_Speed: db $00,$F0,$F0,$F0,$00,$10,$10,$10
Y_Speed: db $10,$10,$00,$F0,$F0,$F0,$00,$10

Tilemap: db $06,$08,$04,$08,$06,$08,$04,$08

; YXPPCCCT format, currently using palette E
Properties: db $BD,$FD,$7D,$7D,$3D,$3D,$3D,$BD

OAM: db $40,$44,$48,$4C,$50,$54,$58,$5C,$60,$64,$68,$6C,$80,$84,$88,$8C,$B0,$B4,$B8,$BC

!cluster_speed_y      = $1E52|!Base2
!cluster_speed_x      = $1E66|!Base2
!cluster_speed_y_frac = $1E7A|!Base2
!cluster_speed_x_frac = $1E8E|!Base2

if !SA1 == 0
    !DanmakuDir   = $7EC300
    !DanmakuTimer = $7EC314
else
    !DanmakuDir   = $400000
    !DanmakuTimer = $400014
endif

Offscreen:
    SEP #$20        ; 8 bit A
    LDA #$00        ; kill sprite
    STA !cluster_num,y
    RTL

print "MAIN ",pc
    LDA !cluster_y_low,y    ; y pos (low)
    STA $00                 ; store to scratch
    LDA !cluster_y_high,y   ; y pos (high)
    STA $01
    LDA !cluster_x_low,y    ; x pos (low)
    STA $02
    LDA !cluster_x_high,y   ; x pos (high)
    STA $03

    REP #$20                ; 16 bit A
    LDA $00                 ; sprite y
    SEC : SBC $1C           ; layer 1 y
    BMI Offscreen           ; kill self if offscreen
    CMP #$00F0
    BCS Offscreen
    STA $00

    LDA $02                 ; sprite x
    SEC : SBC $1A           ; layer 1 x
    BMI Offscreen           ; kill self if offscreen
    CMP #$00F0
    BCS Offscreen
    STA $01

    SEP #$20                ; 8 bit A

Graphics:
    LDX.w OAM,y             ; Get OAM index.
    LDA $00                 ; \ Set y position of tile
    STA $0201|!Base2,x      ; /
    LDA $01                 ; \ Set x position of tile
    STA $0200|!Base2,x      ; /

    PHX                     ; \ Set tile to use
    TYX                     ;  |
    LDA !DanmakuDir,x       ;  |
    TAX                     ;  |
    LDA Tilemap,x           ;  |
    PLX                     ;  |
    STA $0202|!Base2,x      ; /
    PHX                     ; \ 
    TYX                     ;  |
    LDA !DanmakuDir,x       ;  |
    TAX                     ;  |
    LDA Properties,x        ;  | Set tile properties
    PLX                     ;  |
    STA $0203|!Base2,x      ; /

    PHX
    TXA
    LSR
    LSR
    TAX
    LDA #$02
    STA $0420|!Base2,x
    PLX

    LDA $9D                        ; Branch if sprites locked
    BNE Return

    JSR Collision
    BCS NotHit
    PHY
    JSL $00F5B7|!BankB
    PLY
NotHit:
    TYX
    LDA !DanmakuTimer,x                ; Branch if not time for sprite to start moving
    BEQ Continue
    DEC A
    STA !DanmakuTimer,x
    BNE Return
Continue:
    LDA !DanmakuDir,x
    TAX
    LDA X_Speed,x
    STA !cluster_speed_x,y
    LDA Y_Speed,x
    STA !cluster_speed_y,y

    JSR Speed
Return:
    RTL

Collision:              ; checks collision for 16x16 cluster sprites
    PHX

    LDA $94
    SEC                     ; checks if mario's in range horizontally
    SBC !cluster_x_low,y    
    CLC : ADC #$0A                
    CMP #$14                
    BCS NoContact           ; return if not
    LDA #$18
    LDX $73                 ; mario is ducking flag
    BNE ContactContinue     ; collision box = 10 if so
    LDX $19                 ; mario powerup
    BEQ ContactContinue     ; collision box = 10 if small
    LDA #$24                ; else collision box = 20

ContactContinue:
    STA $00                 ; store to scratch
    LDA $96                 ; mario y (screen)
    SEC
    SBC !cluster_y_low,y    ; sprite y
    CLC
    ADC #$1C                ; offset by 10 so as to not have overflow issues; was offset earlier too
    CMP $00
                            ; carry = clear (positive return)
NoContact:
    PLX
    RTS

Speed:
    LDA !cluster_speed_y,y          ; sprite y speed
    ASL #4
    CLC
    ADC !cluster_speed_y_frac,y     ; accumulating fraction bits for y speed
    STA !cluster_speed_y_frac,y     ; set it
    PHP
    LDA !cluster_speed_y,y          ; sprite y speed
    LSR #4
    CMP #$08
    LDX #$00
    BCC .speed1
    ORA #$F0
    DEX

.speed1
    PLP
    ADC !cluster_y_low,y            ; sprite y (low)
    STA !cluster_y_low,y            ; set it
    TXA
    ADC !cluster_y_high,y           ; sprite y (high)
    STA !cluster_y_high,y           ; set it

    LDA !cluster_speed_x,y          ; sprite x speed
    ASL #4
    CLC
    ADC !cluster_speed_x_frac,y     ; accumulating fraction bits for x speed
    STA !cluster_speed_x_frac,y     ; set it
    PHP
    LDA !cluster_speed_x,y          ; sprite x speed
    LSR #4
    CMP #$08
    LDX #$00
    BCC .speed2
    ORA #$F0
    DEX

.speed2
    PLP
    ADC !cluster_x_low,y            ; sprite x (low)
    STA !cluster_x_low,y            ; set it
    TXA
    ADC !cluster_x_high,y           ; sprite x (high)
    STA !cluster_x_high,y           ; set it
    RTS
