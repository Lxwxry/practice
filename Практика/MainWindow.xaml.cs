using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Linq;
using System.Threading.Tasks.Sources;

namespace Practice1
{
    public partial class MainWindow : Window
    {
        Process? notepad;
        CancellationTokenSource? loadCts;
        string? currentFile;
        object lockObj = new object();
        long counter = 0;
        CancellationTokenSource? semCts;

        public MainWindow()
        {
            InitializeComponent();
            BtnStartNotepad.Click += BtnStartNotepad_Click;
            BtnKillNotepad.Click += BtnKillNotepad_Click;
            BtnOpenFile.Click += BtnOpenFile_Click;
            BtnStartLoad.Click += BtnStartLoad_Click;
            BtnStopLoad.Click += BtnStopLoad_Click;
            BtnStartLock.Click += BtnStartLock_Click;
            BtnResetLock.Click += BtnResetLock_Click;
            BtnStartSemaphore.Click += BtnStartSemaphore_Click;
            BtnResetSemaphore.Click += BtnResetSemaphore_Click;
        }

        void BtnStartNotepad_Click(object s, RoutedEventArgs e)
        {
            if (notepad != null && !notepad.HasExited) return;
            try
            {
                notepad = new Process();
                notepad.StartInfo.FileName = "notepad.exe";
                notepad.EnableRaisingEvents = true;
                notepad.Exited += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LblProcessStatus.Content = "Статус: notepad завершён";
                    });
                };
                notepad.Start();
                LblProcessStatus.Content = "Статус: notepad запущен (PID " + notepad.Id + ")";
            }
            catch (Exception ex)
            {
                LblProcessStatus.Content = "Ошибка: " + ex.Message;
            }
        }

        void BtnKillNotepad_Click(object s, RoutedEventArgs e)
        {
            try
            {
                if (notepad != null && !notepad.HasExited)
                {
                    notepad.Kill(true);
                    LblProcessStatus.Content = "Статус: notepad принудительно завершён";
                }
                else
                {
                    LblProcessStatus.Content = "Статус: not запущен";
                }
            }
            catch (Exception ex)
            {
                LblProcessStatus.Content = "Ошибка: " + ex.Message;
            }
        }

        void BtnOpenFile_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Текстовые файлы|*.txt;*.log;*.csv|Все файлы|*.*";
            if (dlg.ShowDialog() == true)
            {
                currentFile = dlg.FileName;
                LblLoadStatus.Content = "Файл: " + System.IO.Path.GetFileName(currentFile);
            }
        }

        async void BtnStartLoad_Click(object s, RoutedEventArgs e)
        {
            if (currentFile == null)
            {
                LblLoadStatus.Content = "Файл не выбран";
                return;
            }
            BtnStartLoad.IsEnabled = false;
            BtnStopLoad.IsEnabled = true;
            loadCts = new CancellationTokenSource();
            try
            {
                var info = new FileInfo(currentFile);
                long total = info.Length;
                long read = 0;
                PbLoad.Value = 0;
                LblLoadStatus.Content = "Чтение...";
                await Task.Run(async () =>
                {
                    using var fs = new FileStream(currentFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var sr = new StreamReader(fs);
                    string? line;
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        if (loadCts.IsCancellationRequested) break;
                        read += System.Text.Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                        double perc = Math.Min(100, (double)read / total * 100);
                        Dispatcher.Invoke(() =>
                        {
                            PbLoad.Value = perc;
                            LblLoadStatus.Content = $"Чтено: {Math.Round(perc,2)}%";
                        });
                        await Task.Delay(1, loadCts.Token).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                }, loadCts.Token);
                if (loadCts.IsCancellationRequested)
                {
                    LblLoadStatus.Content = "Остановлено пользователем";
                }
                else
                {
                    PbLoad.Value = 100;
                    LblLoadStatus.Content = "Загрузка завершена";
                }
            }
            catch (OperationCanceledException)
            {
                LblLoadStatus.Content = "Остановлено";
            }
            catch (Exception ex)
            {
                LblLoadStatus.Content = "Ошибка: " + ex.Message;
            }
            finally
            {
                BtnStartLoad.IsEnabled = true;
                BtnStopLoad.IsEnabled = false;
                loadCts = null;
            }
        }

        void BtnStopLoad_Click(object s, RoutedEventArgs e)
        {
            loadCts?.Cancel();
        }

        async void BtnStartLock_Click(object s, RoutedEventArgs e)
        {
            BtnStartLock.IsEnabled = false;
            BtnResetLock.IsEnabled = false;
            counter = 0;
            LblCounter.Content = "Счетчик: 0";
            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < 500000; i++)
                {
                    lock (lockObj)
                    {
                        counter++;
                    }
                }
            });
            var t2 = Task.Run(() =>
            {
                for (int i = 0; i < 500000; i++)
                {
                    lock (lockObj)
                    {
                        counter++;
                    }
                }
            });
            await Task.WhenAll(t1, t2);
            Dispatcher.Invoke(() =>
            {
                LblCounter.Content = "Счетчик: " + counter;
                BtnStartLock.IsEnabled = true;
                BtnResetLock.IsEnabled = true;
            });
        }

        void BtnResetLock_Click(object s, RoutedEventArgs e)
        {
            counter = 0;
            LblCounter.Content = "Счетчик: 0";
        }

        async void BtnStartSemaphore_Click(object s, RoutedEventArgs e)
        {
            BtnStartSemaphore.IsEnabled = false;
            BtnResetSemaphore.IsEnabled = false;
            PbSemaphore.Value = 0;
            LblSemaphore.Content = "Обработка...";
            semCts = new CancellationTokenSource();
            var items = Enumerable.Range(1,10).ToList();
            var sem = new SemaphoreSlim(3);
            int completed = 0;
            var tasks = new List<Task>();
            foreach (var it in items)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await sem.WaitAsync(semCts.Token);
                    try
                    {
                        await Task.Delay(700, semCts.Token);
                        Interlocked.Increment(ref completed);
                        Dispatcher.Invoke(() =>
                        {
                            PbSemaphore.Value = completed;
                            LblSemaphore.Content = $"Обработано {completed} из {items.Count}";
                        });
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, semCts.Token));
            }
            try
            {
                await Task.WhenAll(tasks);
                Dispatcher.Invoke(() =>
                {
                    LblSemaphore.Content = "Готово";
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    LblSemaphore.Content = "Прервано";
                });
            }
            finally
            {
                BtnStartSemaphore.IsEnabled = true;
                BtnResetSemaphore.IsEnabled = true;
                semCts = null;
                sem.Dispose();
            }
        }

        void BtnResetSemaphore_Click(object s, RoutedEventArgs e)
        {
            semCts?.Cancel();
            PbSemaphore.Value = 0;
            LblSemaphore.Content = "Готов";
        }
    }
}
