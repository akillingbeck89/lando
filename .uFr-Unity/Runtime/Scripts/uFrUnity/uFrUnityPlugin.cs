using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static uFrUnity.uFApi;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;

namespace uFrUnity
{

	public class uFrUnityPlugin : MonoBehaviour
	{
		[System.Serializable]
		public class ReaderConnection
		{
			public uFReader Reader = default;
			public ConnectionInfo Data = new ConnectionInfo();
			public string ReaderSN = default;

			public string LastReadCardUID = default;

			public bool Connected => Reader != null && Reader.opened;
			public bool IsReady => Data != null && Data.CardUID != null;
			public bool CardConnected = false;

			public void ResetCardReads()
			{
				Data = null;
				LastReadCardUID = null;
			}

			public void Disconnected()
			{
				Reader?.close();
				Reader = null;
			}
		}

		public event System.Action<SuccessfulRead> OnReadData = default;
		private ConcurrentDictionary<int, ReaderConnection> m_activeReaders = new ConcurrentDictionary<int, ReaderConnection>();
		private ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
		private ConcurrentQueue<string> Info = new ConcurrentQueue<string>();
		private ConcurrentQueue<SuccessfulRead> SuccessulReads = new ConcurrentQueue<SuccessfulRead>();
		public List<ReaderConnection> m_readers = new List<ReaderConnection>();

		private bool m_isBusy = false;
		private bool m_checkingInfo = false;
		private CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();



		private void Awake()
		{
			Task.Run(Discover, m_cancellationTokenSource.Token);
			Task.Run(HandleDisconnects, m_cancellationTokenSource.Token);
		}

		private async void HandleDisconnects()
		{
			while (!m_cancellationTokenSource.IsCancellationRequested)
			{
				// A reader was disconnected
				for (int i = 0; i < m_activeReaders.Count; i++)
				{
					if (m_activeReaders.ContainsKey(i))
					{
						if (m_activeReaders.TryGetValue(i, out ReaderConnection conn))
						{
							if (Ok(conn.Reader.ReaderStillConnected(out var returnVal), out var status))
							{
								if (returnVal == 0)
								{
									if (!m_activeReaders.TryRemove(i, out ReaderConnection _))
									{
										Errors.Enqueue($"Failed to remove Disconnected reader {i} from active readers");
									}

								}
							}
							else
							{
								Errors.Enqueue($"Failed to call ReaderStillConnected {i} from active readers {status}");
							}
						}
					}
				}

				await Task.Delay(100);
			}
		}
		private async void Discover()
		{
			Info.Enqueue("Discovery Thread Started");
			while (!m_cancellationTokenSource.IsCancellationRequested)
			{
				try
				{
					int currentReadersDetected = ReaderCount();

					if (currentReadersDetected != m_activeReaders.Count)
					{
						if (currentReadersDetected > m_activeReaders.Count)
						{
							// A reader was connected
							for (int i = 0; i < currentReadersDetected; i++)
							{
								if (!m_activeReaders.ContainsKey(i))
								{
									uFReader reader = new uFReader(i);
									if (Ok(reader.open(), out var status))
									{
										var newConn = new ReaderConnection() { Reader = reader, Data = null, ReaderSN = reader.reader_sn };
										if (!m_activeReaders.TryAdd(i, newConn))
										{
											Errors.Enqueue($"Failed to add new connected reader {i} to active readers");
										}
										else
										{
											_ = Task.Run(() => UpdateCardAndConnectionInfo(newConn));
											_ = Task.Run(() => ReadCard(newConn));
										}
									}
									else
									{
										Errors.Enqueue($"Failed to add new connected reader {i} to active readers {status}");
									}

								}
							}
						}
					}
				}
				 catch(Exception ex)
				{
					Errors.Enqueue(ex.Message);
				}
				

				await Task.Delay(500);
			}
			Info.Enqueue("Discovery Thread Stopped");
		}

		private async void UpdateCardAndConnectionInfo(ReaderConnection conn)
		{
			Info.Enqueue($"UpdateCardAndConnectionInfo Thread Started {conn.ReaderSN}");
			while (!m_cancellationTokenSource.IsCancellationRequested && conn.Connected)
			{
				try
				{
					if (!Ok(GetCardConnectionInfo(ref conn, ref m_checkingInfo), out var status)) {
						if (status == DL_STATUS.UFR_NO_CARD || status == DL_STATUS.UFR_FT_STATUS_ERROR_5 || status == DL_STATUS.UFR_PARAMETERS_ERROR)
						{
							conn.CardConnected = false;
						} else
						{
							Errors.Enqueue($"Failed to GetCardConnectionInfo {conn.ReaderSN} {status}");
						}
					} else
					{
						conn.CardConnected = true;
					}
				}
				catch (Exception ex)
				{
					Errors.Enqueue(ex.Message);
				}

				await Task.Delay(100);
			}

			Info.Enqueue($"UpdateCardAndConnectionInfo Thread Stopped {conn.ReaderSN}");
		}

		private async void ReadCard(ReaderConnection conn)
		{
			Info.Enqueue($"ReadCard Thread Started {conn.ReaderSN}");
			while (!m_cancellationTokenSource.IsCancellationRequested && conn.Connected)
			{
				if (conn.CardConnected && conn.IsReady && conn.LastReadCardUID != conn.Data.CardUID)
				{
					if (!Ok(Read(ref conn, ref m_isBusy), out var status))
					{
						//Errors.Enqueue($"Failed to Read card info for reader: {conn.ReaderSN} {status}");
					} else
					{
						SuccessulReads.Enqueue(new SuccessfulRead() { ReaderId = conn.ReaderSN, ReaderData = conn.Data.Data });
					}
				}

				await Task.Delay(200);
			}
			Info.Enqueue($"ReadCard Thread Stopped {conn.ReaderSN}");
		}

		private void Update()
		{
			while(SuccessulReads.TryDequeue(out SuccessfulRead readResult))
			{
				OnReadData?.Invoke(readResult);
			}

			while(Errors.TryDequeue(out string error))
			{
				Debug.LogWarning(error);
			}

			while(Info.TryDequeue(out string info))
			{
				Debug.Log(info);
			}

			m_readers = m_activeReaders.Values.ToList();
		}

		private void OnDestroy()
		{
			m_cancellationTokenSource.Cancel();
			foreach (var reader in m_activeReaders)
			{
				Close(reader.Value);
			}
		}
	}
}
