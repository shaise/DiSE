/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
  ******************************************************************************
  * @attention
  *
  * <h2><center>&copy; Copyright (c) 2021 STMicroelectronics.
  * All rights reserved.</center></h2>
  *
  * This software component is licensed by ST under Ultimate Liberty license
  * SLA0044, the "License"; You may not use this file except in compliance with
  * the License. You may obtain a copy of the License at:
  *                             www.st.com/SLA0044
  *
  ******************************************************************************
  */
/* USER CODE END Header */

/* Define to prevent recursive inclusion -------------------------------------*/
#ifndef __MAIN_H
#define __MAIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* Includes ------------------------------------------------------------------*/
#include "stm32f1xx_hal.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Exported types ------------------------------------------------------------*/
/* USER CODE BEGIN ET */

/* USER CODE END ET */

/* Exported constants --------------------------------------------------------*/
/* USER CODE BEGIN EC */

/* USER CODE END EC */

/* Exported macro ------------------------------------------------------------*/
/* USER CODE BEGIN EM */

/* USER CODE END EM */

/* Exported functions prototypes ---------------------------------------------*/
void Error_Handler(void);

/* USER CODE BEGIN EFP */

/* USER CODE END EFP */

/* Private defines -----------------------------------------------------------*/
#define KbdI0_Pin GPIO_PIN_0
#define KbdI0_GPIO_Port GPIOA
#define KbdI1_Pin GPIO_PIN_1
#define KbdI1_GPIO_Port GPIOA
#define KbdI2_Pin GPIO_PIN_2
#define KbdI2_GPIO_Port GPIOA
#define KbdI3_Pin GPIO_PIN_3
#define KbdI3_GPIO_Port GPIOA
#define KbdI4_Pin GPIO_PIN_4
#define KbdI4_GPIO_Port GPIOA
#define KbdI5_Pin GPIO_PIN_5
#define KbdI5_GPIO_Port GPIOA
#define KbdI6_Pin GPIO_PIN_6
#define KbdI6_GPIO_Port GPIOA
#define KbdL0_Pin GPIO_PIN_0
#define KbdL0_GPIO_Port GPIOB
#define KbdL1_Pin GPIO_PIN_10
#define KbdL1_GPIO_Port GPIOB
#define KbdL2_Pin GPIO_PIN_11
#define KbdL2_GPIO_Port GPIOB
#define KbdL3_Pin GPIO_PIN_12
#define KbdL3_GPIO_Port GPIOB
#define KbdL4_Pin GPIO_PIN_13
#define KbdL4_GPIO_Port GPIOB
#define KbdL5_Pin GPIO_PIN_14
#define KbdL5_GPIO_Port GPIOB
#define KbdL6_Pin GPIO_PIN_15
#define KbdL6_GPIO_Port GPIOB
#define EncoderA_Pin GPIO_PIN_8
#define EncoderA_GPIO_Port GPIOA
#define EncoderB_Pin GPIO_PIN_9
#define EncoderB_GPIO_Port GPIOA
#define KbdO0_Pin GPIO_PIN_3
#define KbdO0_GPIO_Port GPIOB
#define KbdO1_Pin GPIO_PIN_4
#define KbdO1_GPIO_Port GPIOB
#define KbdO2_Pin GPIO_PIN_5
#define KbdO2_GPIO_Port GPIOB
#define KbdO3_Pin GPIO_PIN_6
#define KbdO3_GPIO_Port GPIOB
#define KbdO4_Pin GPIO_PIN_7
#define KbdO4_GPIO_Port GPIOB
#define KbdO5_Pin GPIO_PIN_8
#define KbdO5_GPIO_Port GPIOB
#define KbdO6_Pin GPIO_PIN_9
#define KbdO6_GPIO_Port GPIOB
/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */

/************************ (C) COPYRIGHT STMicroelectronics *****END OF FILE****/
