;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; Cave-In Generator, by yoshicookiezeus
;; Cluster sprite version by telinc1
;;
;; Description: Spawns Cave-In Blocks at random x positions at a set height.
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

; If your ceiling is too high or !BlockHeight is too low, the blocks could
; get "killed" when they spawn. If that happens, try increasing the !BlockHeight.

!BlockNumber = $00      ; Cluster sprite number the cave-in block sprite is inserted as in list.txt
!BlockHeight = $00C0    ; The Y position at which blocks are spawned.

print "INIT ",pc
print "MAIN ",pc
    PHB : PHK : PLB
    JSR GeneratorCode
    PLB
    RTL

GeneratorCode:
    LDA $9D                 ; if sprites locked, don't spawn anything
    BNE Return

    LDA $14                 ;\ if not yet time to spawn new block,
    AND #$0F                ; |
    BNE Return              ;/ return

    LDA #$20                ;\ shake ground
    STA $1887|!Base2        ;/

    LDY #$13                ; get free cluster sprite num
-   LDA !cluster_num,y
    BEQ +                   ; if 0, got one
    DEY                     ; else keep looping
    BPL -
    BRA Return              ; if none available, return

+   LDA.b #!BlockNumber+!ClusterOffset      ; set cluster sprite number
    STA !cluster_num,y

    LDA #$01                ; run cluster sprite code
    STA $18B8|!Base2

    TYX

    LDA.b #!BlockHeight         ;\ set new sprite y position
    STA !cluster_y_low,x        ; |

    if !BlockHeight>>8&$FF == 0
        STZ !cluster_y_high,x   ;/
    else
        LDA.b #!BlockHeight>>8
        STA !cluster_y_high,x   ;/
    endif

    JSL $01ACF9|!BankB      ; random number generation subroutine
    REP #$20                ;\  set new sprite x position
    LDA $148B|!Base2        ; |
    AND #$00FF              ; |
    CLC : ADC $1A           ; |
    SEP #$20                ; |
    STA !cluster_x_low,x    ; |
    XBA                     ; |
    STA !cluster_x_high,x   ;/

    STZ $1E52|!Base2,x      ; set no Y speed

    LDA $148B|!Base2        ;\  set options randomly
    LSR #2                  ; | could just do AND #$01, but this gives the same
    AND #$01                ; | distribution as in the original sprite
    STA $0F72|!Base2,x      ;/
Return:
    RTS
