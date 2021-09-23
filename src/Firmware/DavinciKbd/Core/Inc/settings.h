/*
 * flash.h
 *
 *  Created on: Aug 25, 2021
 *      Author: shai
 */

#ifndef INC_SETTINGS_H_
#define INC_SETTINGS_H_

#include "kbd_led.h"
#include "keycode.h"

typedef struct {
	uint32_t settingsSize;
	sKbdCodeData codeTable[MAX_NUM_KEYS];
	sJogCodeData jogTable[MAX_JOG_CODES];
	uint8_t jogTypes[JOG_MODES];
	uint16_t LongPressPeriod;
	uint16_t DoubleClickPeriod;
	//int dummy; // force flash invalidation
} sGeneralSettings;

extern sGeneralSettings gSettings;

void InitSettings(void);
int SaveSettings(void);
int FactoryDefault(void);

#endif /* INC_SETTINGS_H_ */
