;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Cave-In Block, by telinc1
;; mimcs the Cave-In Block by yoshicookiezeus
;; uses routines from the SMB3 Statue Lasers and Foo
;;
;; A block that falls, bounces when it hits the ground, and then falls
;; offscreen. It is 16x16 if the extra bit is set, and 32x32 if it isn't.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

!Gravity = $40                      ; Strength of gravity.
!FallSpeed = $E0                    ; Y speed after hitting the ground.

!AppearAboveGround = 1              ; Set to 1 if you want the block to appear above the ground
!HurtMario = 1                      ; Set to 1 if you want the block to hurt Mario
!HurtAfterFall = 0                  ; Set to 1 if you want the block to hurt Mario even after hitting the ground

!FallSFX = $09                      ; Sound effect to play when the sprite hits the ground
!FallBank = $1DFC

!SmallTile = $E0                    ; Tile to use for small blocks
!Properties = $03                   ; Sprite properties, YXPPCCCT format

X_Disp:     db $00,$10,$00,$10      ;\  Tilemap for the big blocks
Y_Disp:     db $00,$00,$10,$10      ; |
TilemapBig: db $CC,$CE,$EC,$EE      ;/

; Names for some sprite tables and variables, do not touch.
!cluster_options = $0F72|!Base2             ; Bitfield, ---- --ds, d: dead if set, s: small if set
!cluster_off_screen = $0F9A|!Base2          ; Set to 1 if the sprite is offscreen
!cluster_speed_y = $1E52|!Base2
!cluster_speed_y_frac = $1E8E|!Base2

print "MAIN ",pc
Main:
    JSR Graphics                    ; Draw block (doesn't return if offscreen)
    LDA $9D                         ;\ If sprites locked,
    BNE .return                     ;/ return.
    JSR UpdateClusterY              ; Update Y position.

    if !HurtMario == 1 && !HurtAfterFall == 1
        JSR Collision
        BCS +
        JSL $00F5B7|!BankB
    endif

+   LDA !cluster_options,y          ; Return if we're dead.
    AND #$02
    BNE .return

    if !HurtMario == 1 && !HurtAfterFall == 0
        JSR Collision
        BCS +
        JSL $00F5B7|!BankB
    endif

+   LDA #$10                        ; Get the Map16 tile $10 pixels below the sprite.
    JSR ObjectInteraction
    LDA $18D7|!Base2
    XBA : LDA $1693|!Base2
    REP #$20
-   ASL A                           ; Check its "acts like" value.
    BMI .highPage
    ADC $06F624|!BankB
    STA $0D
    SEP #$20
    LDA $06F626|!BankB
    STA $0F
.readTile
    REP #$20
    LDA [$0D]
    CMP #$0200
    BCS -                           ; At this point, A = Map16 number in the range [0; 1FF]
    SEP #$20
    XBA
    BEQ .return                     ; By default, everything on page 0 is non-solid.
    LDA !cluster_options,y          ;\  Set sprite to "dead".
    ORA #$02                        ; |
    STA !cluster_options,y          ;/
    LDA.b #!FallSpeed               ;\ Set new sprite Y speed.
    STA !cluster_speed_y,y          ;/
    LDA.b #!FallSFX                 ;\ Play sound effect.
    STA.w !FallBank|!Base2          ;/
.return
    RTL

.highPage
    ADC $06F63A|!BankB
    ORA #$8000
    STA $0D
    SEP #$20
    LDA $06F63C|!BankB
    STA $0F
    BRA .readTile

KillSprite:
    LDA #$00                        ; Destroy current sprite.
    STA !cluster_num,y

    PLA : PLA                       ; Destructive return to PIXI's code.
    RTL

Graphics:
    JSR GetDrawInfo                 ; get positions relative to screen border.
    LDA !cluster_off_screen,y       ; Kill if offscreen.
    BNE KillSprite
    LDX #$40                        ; Find a blank OAM slot, starting at $0240.
    JSR GetOAMSlot

    LDA !cluster_options,y          ; Use big block's graphics routine if we're not small.
    AND #$01
    BEQ .bigBlock

    LDA $00                         ; Graphics routine for the small block
    STA $0200|!Base2,x
    LDA $01
    STA $0201|!Base2,x
    LDA.b #!SmallTile
    STA $0202|!Base2,x
    LDA.b #!Properties
    ORA $64
    if !AppearAboveGround == 0
        AND #$CF                    ; Clear priority bits.
    endif
    STA $0203|!Base2,x

    TXA : LSR #2 : TAX
    LDA #$02                        ; Set object size to big (16x16).
    ORA $02                         ; Pull in high bit of X from GetDrawInfo.
    STA $0420|!Base2,x
.return
    RTS

; Graphics routine for the big block
.bigBlock
    PHY
    LDY #$03
-   LDA $02
    STA $03

    LDA $00
    CLC : ADC X_Disp,y
    BCS ..checkHigh
..continueGraphics
    STA $0200|!Base2,x

    LDA $01
    CLC : ADC Y_Disp,y
    STA $0201|!Base2,x

    LDA TilemapBig,y
    STA $0202|!Base2,x

    LDA.b #!Properties
    ORA $64
    if !AppearAboveGround == 0
        AND #$CF                    ; Clear priority bits.
    endif
    STA $0203|!Base2,x

    PHX
    TXA : LSR #2 : TAX
    LDA #$02                        ; Set object size to big (16x16).
    ORA $03                         ; Pull in high bit of X from GetDrawInfo and ..checkHigh.
    STA $0420|!Base2,x
    PLX

    INX #4                          ; Increase index to OAM.
..skipTile
    DEY
    BPL -
    PLY
    RTS

..checkHigh
    STZ $03
    PHA                             ; If adding the X displacement overflew the X position, then
    LDA $02                         ; the sprite is leaving either side of the screen. If it's leaving
    BNE +                           ; the right side, then we can skip the right portion of the sprite.
    PLA                             ; If it's leaving the left side, then the high bit of X will be
    BRA ..skipTile                  ; set (and the overflow will properly align us), so we don't want to
                                    ; skip drawing the right side of the sprite. GetDrawInfo sets $02 to
+   PLA                             ; the high bit of X (in OAM, the X position is 9 bits long; the MSB
    BRA ..continueGraphics          ; is stored in the high table along with the sprite size).

;;;;;;;;;;;;;;;;;;;;;
;; Shared Routines ;;
;;;;;;;;;;;;;;;;;;;;;

; Finds a free slot in OAM, starting at slot X/4 (X: initial OAM index).
GetOAMSlot:
    TXA
    CMP #$FC
    BEQ .break
-   LDA $0201|!Base2,x
    CMP #$F0
    BEQ .break
    INX #4
    CPX #$FC
    BNE -
.break
    RTS

; Sets the offscreen flag and calculates sprite position relative to screen border.
GetDrawInfo:
    TYX
    LDA $02FF50|!BankB,x
    TAX
GetDrawInfoNoOAM:
    LDA #$00
    STA !cluster_off_screen,y
    STA $02
    LDA !cluster_x_high,y
    XBA
    LDA !cluster_x_low,y
    REP #$20
    SEC : SBC $1A
    CLC : ADC #$0040
    CMP #$0180
    SEP #$20
    ROL A                               ; Parts of the routine are adapted from the real
    AND #$01                            ; GetDrawInfo to have more accurate off-screen detection.
    STA !cluster_off_screen,y
    BNE .invalid
    LDA !cluster_x_high,y
    XBA
    LDA !cluster_x_low,y
    REP #$20
    SEC : SBC $1A
    CMP #$FFC0
    SEP #$20
    BCC .nopeX
.nopeX
    LDA !cluster_y_low,y
    CLC : ADC #$1E
    PHP
    CMP $1C
    ROL $00
    PLP
    LDA !cluster_y_high,y
    ADC #$00
    LSR $00
    SBC $1D
    BEQ .nopeY
    LDA #$01
    STA !cluster_off_screen,y
.nopeY
    LDA !cluster_x_low,y
    SEC : SBC $1A
    STA $00
    LDA !cluster_x_high,y
    SBC $1B
    BEQ .noHighBit
    LDA #$01
    STA $02
.noHighBit
    LDA !cluster_y_low,y
    SEC : SBC $1C
    STA $01
.invalid
    RTS

; Updates the cluster sprite's Y position with gravity.
; Most of the routine is adapted from $01801A (UpdateYPosNoGrvty).
; Everything after .finish is adapted from $019049,
; part of $019032 (SubUpdateSprPos), a.k.a $01802A (UpdateSpritePos).
UpdateClusterY:
    LDA !cluster_speed_y,y
    BEQ .finish
    ASL #4
    CLC
    ADC !cluster_speed_y_frac,y
    STA !cluster_speed_y_frac,y
    PHP
    LDX #$00
    LDA !cluster_speed_y,y
    LSR #4
    CMP #$08
    BCC $03
    ORA #$F0
    DEX
    PLP
    ADC !cluster_y_low,y
    STA !cluster_y_low,y
    TXA
    ADC !cluster_y_high,y
    STA !cluster_y_high,y
.finish
    LDA !cluster_speed_y,y          ; This is responsible for applying gravity to the sprite.
    CLC : ADC #$03
    STA !cluster_speed_y,y
    BMI .return
    CMP.b #!Gravity
    BCC .return
    LDA.b #!Gravity
    STA !cluster_speed_y,y
.return
    RTS

; Gets the Map16 tile at the sprite's coordinates.
; Ripped from the SMB3 Statue Lasers.
; A contains the offset from the sprite's Y position (used for getting the tile below the sprite).
ObjectInteraction:
    PHY
    INC A : STA $00             ; INC A emulates the default sprite clipping

    LDA !cluster_options,y      ; increase offset by $10 if we're big
    AND #$01
    BNE +
        LDA $00
        CLC : ADC #$10
        STA $00
+   LDA !cluster_y_low,y
    CLC : ADC $00               ; offset Y + clipping Y
    STA $0C
    AND #$F0
    STA $00
    LDA !cluster_y_high,y
    ADC #$00
    STA $0D
    REP #$20
    LDA $0C
    CMP #$01B0
    SEP #$20
    BCS .return
    LDA !cluster_x_low,y
    CLC : ADC #$01              ; clipping X
    STA $0A
    STA $01
    LDA !cluster_x_high,y
    ADC #$00
    STA $0B
    BMI .return
    CMP $5D
    BCS .return
    LDA $01
    LSR #4
    ORA $00
    STA $00
    LDX $0B
    LDA.l $00BA60|!BankB,x
    CLC : ADC $00
    STA $05
    LDA.l $00BA9C|!BankB,x
    ADC $0D
    STA $06
    LDA.b #!BankA>>16
    STA $07
    LDX $15E9|!Base2
    LDA [$05]               ; read map16 low byte table ($7EC800)
    STA $1693|!Base2        ; Block you're interacting with (low byte) goes into $1693
    INC $07                 ; Switch to map16 high byte table ($7FC800)
    LDA [$05]               ; Load map16 high byte
    STA $18D7|!Base2        ; Block you're interacting with (high byte) goes into $18D7
    PLY
    RTS

.return
    LDX $0F
    STZ $1693|!Base2
    STZ $1694|!Base2
    PLY
    RTS

; Checks if a 16x16 cluster sprite is interacting with Mario.
; Adapted to support a 32x16 hitbox.
Collision:
    PHX

    STZ $01
    LDA !cluster_options,y
    AND #$01
    BNE +
        LDA #$10
        STA $01
+   LDA $01
    CLC : ADC #$14
    STA $02

    LDA $94
    SEC                     ; checks if Mario's in range horizontally
    SBC !cluster_x_low,y
    CLC : ADC #$0A
    CMP $02
    BCS .noContact          ; return if not
    LDA #$18
    LDX $73                 ; mario is ducking flag
    BNE .continue           ; collision box = 10 if so
    LDX $19                 ; mario powerup
    BEQ .continue           ; collision box = 10 if small
    LDA #$24                ; else collision box = 20

.continue
    CLC : ADC $01
    STA $00                 ; store to scratch
    LDA $96                 ; mario y (screen)
    SEC
    SBC !cluster_y_low,y    ; sprite y
    CLC
    ADC #$1C                ; offset by 10 so as to not have overflow issues; was offset earlier too
    CMP $00
                            ; carry = clear (positive return)
.noContact
    PLX
    RTS
