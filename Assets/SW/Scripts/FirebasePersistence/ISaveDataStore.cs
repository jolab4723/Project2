using System.Threading.Tasks;

namespace Core
{
    public interface ISaveDataStore
    {
        /// <summary>
        /// 지정한 사용자와 데이터 종류에 해당하는 저장 봉투를 불러옵니다.
        /// </summary>
        Task<SaveDataReadResult> LoadAsync(
            string userId,
            SaveDataDefinition definition);

        /// <summary>
        /// 지정한 사용자와 데이터 종류에 저장 봉투를 기록합니다.
        /// </summary>
        Task<SaveDataOperationResult> SaveAsync(
            string userId,
            SaveDataDefinition definition,
            SaveDataEnvelope envelope);

        /// <summary>
        /// 지정한 사용자와 데이터 종류에 해당하는 저장 데이터를 삭제합니다.
        /// </summary>
        Task<SaveDataOperationResult> DeleteAsync(
            string userId,
            SaveDataDefinition definition);
    }
}

