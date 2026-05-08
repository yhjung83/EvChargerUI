using System;
using System.Collections.Generic;

namespace EvChargerUI.Services
{
    /// <summary>
    /// 채비 모델명과 팔 이동형 타입(LR/UDLR)을 매핑하는 서비스
    /// </summary>
    public static class ChaeviModelMappingService
    {
        private static readonly Dictionary<string, string> _modelToArmMovableTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 좌우(LR) 모델들
            { "DVC-3FS100N-U", "LR" },
            { "DVC-3FNHOC-U", "LR" },
            
            // 상하좌우(UDLR) 모델들
            { "DVC-3FS100W-U", "UDLR" },
        };

        /// <summary>
        /// 모델명으로부터 팔 이동형 타입을 가져옵니다.
        /// </summary>
        /// <param name="modelName">채비 모델명</param>
        /// <returns>팔 이동형 타입 (LR, UDLR, 또는 NONE)</returns>
        public static string GetArmMovableType(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "NONE";
            }

            if (_modelToArmMovableTypeMap.TryGetValue(modelName, out string armMovableType))
            {
                return armMovableType;
            }

            return "NONE";
        }

        /// <summary>
        /// 지원되는 모든 모델명 목록을 가져옵니다.
        /// </summary>
        /// <returns>모델명 목록</returns>
        public static List<string> GetAllModelNames()
        {
            return new List<string>(_modelToArmMovableTypeMap.Keys);
        }

        /// <summary>
        /// 특정 팔 이동형 타입에 해당하는 모델명 목록을 가져옵니다.
        /// </summary>
        /// <param name="armMovableType">팔 이동형 타입 (LR 또는 UDLR)</param>
        /// <returns>해당 타입의 모델명 목록</returns>
        public static List<string> GetModelNamesByArmMovableType(string armMovableType)
        {
            var result = new List<string>();
            foreach (var kvp in _modelToArmMovableTypeMap)
            {
                if (string.Equals(kvp.Value, armMovableType, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(kvp.Key);
                }
            }
            return result;
        }
    }
}

