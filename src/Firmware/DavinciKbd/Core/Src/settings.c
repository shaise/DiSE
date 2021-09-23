/*
 * flash.c
 *
 *  Created on: Aug 25, 2021
 *      Author: shai
 *
 *  Read and write from/to flash
 */

#include "main.h"
#include "string.h"
#include "settings.h"


uint8_t bytes_temp[4];

#define FLASH_RESERVE_PAGES 2  // reserve memory for data saving, each page is 1K
#define MEMSIZE_128K           // most stm32f103c8T have 128K instead of the 64K stated

#ifdef MEMSIZE_128K
#define FLASH_ADDR	(0x08000000 + FLASH_PAGE_SIZE * (128 - FLASH_RESERVE_PAGES))
#else
#define FLASH_ADDR	(0x08000000 + FLASH_PAGE_SIZE * (64 - FLASH_RESERVE_PAGES))
#endif

static int FlashWrite (void *data, int len);
static int FlashRead (void *data, int len);


static const sGeneralSettings gDefaultSettings = {
	0,
	{
		/* 1 */ { KEY_F9,0,51,0,0,0,0 },
		/* 2 */ { KEY_Quote,0,0,0,0,0,0 },
		/* 3 */ { KEY_I,0,0,4,12,4,0 },
		/* 4 */ { KEY_Y,2,0,3,4,3,20 },
		/* 5 */ { KEY_G,1,0,3,4,3,20 },
		/* 6 */ { 0,0,0,0,0,0,0 },
		/* 7 */ { KEY_4,1,0,0,0,0,0 },
		/* 8 */ { KEY_F12,2,0,0,0,0,0 },
		/* 9 */ { KEY_F12,0,0,0,0,0,0 },
		/* 10 */ { 0,0,0,0,0,0,0 },
		/* 11 */ { KEY_U,2,0,3,4,3,20 },
		/* 12 */ { KEY_H,1,0,3,4,3,20 },
		/* 13 */ { KEY_G,2,0,3,23,1,0 },
		/* 14 */ { 0,0,0,0,0,0,0 },
		/* 15 */ { KEY_F10,2,0,0,0,0,0 },
		/* 16 */ { KEY_F10,0,0,0,0,0,0 },
		/* 17 */ { KEY_O,0,0,4,18,4,0 },
		/* 18 */ { KEY_G,2,0,3,4,3,20 },
		/* 19 */ { 0,0,0,0,0,0,0 },
		/* 20 */ { 0,0,0,0,0,0,0 },
		/* 21 */ { KEY_Escape,0,0,4,29,1,0 },
		/* 22 */ { 0,0,0,0,0,0,0 },
		/* 23 */ { 0,0,0,0,0,0,0 },
		/* 24 */ { KEY_7,0,2,0,0,0,0 },
		/* 25 */ { KEY_4,0,2,0,0,0,0 },
		/* 26 */ { KEY_1,0,2,0,0,0,0 },
		/* 27 */ { KEY_Space,0,0,0,0,0,0 },
		/* 28 */ { 0,0,0,0,0,0,0 },
		/* 29 */ { KEY_S,6,0,0,0,0,0 },
		/* 30 */ { KEY_Backslash,1,0,0,0,0,0 },
		/* 31 */ { KEY_8,0,2,0,0,0,0 },
		/* 32 */ { KEY_5,0,2,0,0,0,0 },
		/* 33 */ { KEY_2,0,2,0,0,0,0 },
		/* 34 */ { KEY_3,1,0,0,0,0,0 },
		/* 35 */ { 0,0,50,0,0,0,1 },
		/* 36 */ { KEY_V,2,0,3,4,3,21 },
		/* 37 */ { 0,0,0,0,0,0,22 },
		/* 38 */ { KEY_9,0,2,0,0,0,0 },
		/* 39 */ { KEY_6,0,2,0,0,0,0 },
		/* 40 */ { KEY_3,0,2,0,0,0,0 },
		/* 41 */ { KEY_4,1,0,0,0,0,0 },
		/* 42 */ { 0,0,50,0,0,0,2 },
		/* 43 */ { 0,0,0,0,0,0,0 },
		/* 44 */ { KEY_Delete,0,0,0,0,0,0 },
		/* 45 */ { 0,0,0,0,0,0,0 },
		/* 46 */ { KEY_Backslash,6,3,1,47,6,0 },
		/* 47 */ { KEY_RightBracket,6,3,1,47,6,0 },
		/* 48 */ { 0,0,0,0,0,0,0 },
		/* 49 */ { 0,0,50,0,0,0,3 },
	},
	{
	    /* 0,0,0 */ { KEY_Right,0,5,50 },
		/* 0,0,1 */ { KEY_Left,0,5,50 },
		/* 0,1,0 */ { KEY_Right,2,2,20 },
		/* 0,1,1 */ { KEY_Left,2,2,20 },
		/* 0,2,0 */ { 0,0,0,0 },
		/* 0,2,1 */ { 0,0,0,0 },
		/* 1,0,0 */ { KEY_Right,0,20,5 },
		/* 1,0,1 */ { KEY_Left,0,20,5 },
		/* 1,1,0 */ { KEY_Right,2,255,25 },
		/* 1,1,1 */ { KEY_Left,2,255,25 },
		/* 1,2,0 */ { 0,0,0,0 },
		/* 1,2,1 */ { 0,0,0,0 },
		/* 2,0,0 */ { KEY_Equals,1,255,25 },
		/* 2,0,1 */ { KEY_Minus,1,255,25 },
		/* 2,1,0 */ { 0,0,0,0 },
		/* 2,1,1 */ { 0,0,0,0 },
		/* 2,2,0 */ { 0,0,0,0 },
		/* 2,2,1 */ { 0,0,0,0 },
		/* 3,0,0 */ { KEY_Period,0,20,5 },
		/* 3,0,1 */ { KEY_Comma,0,20,5 },
		/* 3,1,0 */ { KEY_Period,2,255,25 },
		/* 3,1,1 */ { KEY_Comma,2,255,25 },
		/* 3,2,0 */ { 0,0,0,0 },
		/* 3,2,1 */ { 0,0,0,0 },
		/* 4,0,0 */ { KEY_Equals,5,20,5 },
		/* 4,0,1 */ { KEY_Minus,5,20,5 },
		/* 4,1,0 */ { KEY_Equals,6,255,25 },
		/* 4,1,1 */ { KEY_Minus,6,255,25 },
		/* 4,2,0 */ { 0,0,0,0 },
		/* 4,2,1 */ { 0,0,0,0 },
		/* 5,0,0 */ { KEY_Down,0,255,20 },
		/* 5,0,1 */ { KEY_Up,0,255,20 },
		/* 5,1,0 */ { 0,0,0,0 },
		/* 5,1,1 */ { 0,0,0,0 },
		/* 5,2,0 */ { 0,0,0,0 },
		/* 5,2,1 */ { 0,0,0,0 },
	},
	{ 1, 0, 0, 0 },
	700,
	250

};

sGeneralSettings gSettings;

void InitSettings()
{
	int savedsize = *(uint32_t *)FLASH_ADDR;
	int actualsize = sizeof(sGeneralSettings);
	if (savedsize == actualsize)
		FlashRead(&gSettings, savedsize);
	else
		gSettings = gDefaultSettings;
}

int SaveSettings()
{
	gSettings.settingsSize = sizeof(sGeneralSettings);
	return FlashWrite(&gSettings, gSettings.settingsSize);
}

int FactoryDefault()
{
	gSettings = gDefaultSettings;
	return SaveSettings();
}


static int FlashWrite (void *data, int len)
{

	FLASH_EraseInitTypeDef EraseInitStruct;
	int lenwords;
	uint32_t *dataptr;
	HAL_StatusTypeDef stat;
	uint32_t addr;
	uint32_t PageError;

	HAL_FLASH_Unlock();

    EraseInitStruct.TypeErase   = FLASH_TYPEERASE_PAGES;
    EraseInitStruct.PageAddress = FLASH_ADDR;
    EraseInitStruct.NbPages     = FLASH_RESERVE_PAGES;


	if (HAL_FLASHEx_Erase(&EraseInitStruct, &PageError) != HAL_OK)
	  return HAL_FLASH_GetError ();

	lenwords = (len + 3) / 4;
	dataptr = (uint32_t *)data;
	addr = FLASH_ADDR;

	for (int i = 0; i < lenwords; i++)
	{
		stat = HAL_FLASH_Program(FLASH_TYPEPROGRAM_WORD, addr, *dataptr);
		if (stat != HAL_OK)
			return HAL_FLASH_GetError();

		addr += 4;
		dataptr++;
	}

	HAL_FLASH_Lock();

	return 0;
}

static int FlashRead (void *data, int len)
{
	memcpy(data, (uint8_t *)FLASH_ADDR, len);
	return 0;
}
