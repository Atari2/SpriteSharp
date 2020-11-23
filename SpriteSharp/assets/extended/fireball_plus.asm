;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; fireball_plus
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; behind = 00 -> gravity down
; behind = 01 -> gravity right
; behind = 02 -> gravity up
; behind = 03 -> gravity left
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

tile:
db $2D,$2C,$2D,$2C
prop:
db $F4,$F4,$34,$34

Print "MAIN ",pc
	JSR GFX
	LDA $9D
	BNE return
	JSR hurt
	LDA !extended_timer,x
	BNE +
	LDA #$0F
	STA !extended_timer,x
+
	LDA !extended_behind,x
	BEQ up
	DEC
	BEQ left
	DEC
	BEQ down
	DEC
	BEQ right
	RTL
up:
	LDA #$00
	%ExtendedSpeed()
	RTL
left:
	LDA $1747|!Base2,x
	CMP #$40
	BPL +
	CLC
	ADC #$03
	STA $1747|!Base2,x
	BRA +
	RTL
down:
	LDA $173D|!Base2,x
	CMP #$C1
	BMI +
	SEC
	SBC #$03
	STA $173D|!Base2,x
	BRA +
	RTL
right:
	LDA $1747|!Base2,x
	CMP #$C1
	BMI +
	SEC
	SBC #$03
	STA $1747|!Base2,x
+
	LDA #$01
	%ExtendedSpeed()
return:
	RTL
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;Graphics Routine
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

GFX:
	%ExtendedGetDrawInfo()

	LDA $01
	STA $0200|!Base2,y

	LDA $02
	STA $0201|!Base2,y

	PHX
	LDA !extended_timer,x
	AND #$0C
	LSR #2
	TAX
	LDA tile,x
	STA $0202|!Base2,y
	PLX

	PHX
	LDA !extended_timer,x
	AND #$0C
	LSR #2
	TAX
	LDA prop,x
	ORA $64
	STA $0203|!Base2,y
	PLX

	TYA
	LSR #2
	TAY
	LDA #$00
	STA $0420|!Base2,y

	RTS
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; Extended Hurt 8x8
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; extended sprite -> mario interaction.
hurt:
	LDA $171F|!Base2,x
	CLC
	ADC #$01
	STA $04
	LDA $1733|!Base2,x
	ADC #$00
	STA $0A
	LDA #$06
	STA $06
	STA $07
	LDA $1715|!Base2,x
	CLC
	ADC #$01
	STA $05
	LDA $1729|!Base2,x
	ADC #$00
	STA $0B
	JSL $03B664|!BankB ;mario(B)
	JSL $03B72B|!BankB ;A+B
	BCC .skip
	PHB
	LDA.b #($02|!BankB>>16)
	PHA
	PLB
	PHK
	PEA.w .return-1
	PEA.w $B889-1
	JML $02A469|!BankB
.return
	PLB 
.skip
	RTS
