/*
 * Created by SharpDevelop.
 * User: David
 * Date: 2/12/2018
 * Time: 8:15 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Drawing;
using System.Data.SQLite; //32 bit version is loaded
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Linq;
using System.ComponentModel;
using NBitcoin;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NebliDex
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window
	{
		
		public NotifyIcon trayicon;
		public int last_candle_time = 0; //The utctime of the last candle
		public int chart_timeline = 0; //24 hours, 1 = 7 days
		public double chart_low = 0,chart_high = 0;
		
		//UI Information
		public SolidColorBrush green_candle;
		public SolidColorBrush darkgreen_candle;
		public SolidColorBrush red_candle;
		private LinearGradientBrush default_ui_gradient;
		private Style default_marketbox_style;
		private SolidColorBrush default_canvas_border;
		private SolidColorBrush default_canvas_color;
		public int current_ui_look = 0;
		private SolidColorBrush dark_ui_panel;
		private SolidColorBrush dark_ui_foreground;

		public Window1()
		{
			InitializeComponent();
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			
			//Create a notify icon for the tray
	        trayicon = new System.Windows.Forms.NotifyIcon();
	        trayicon.Icon = new System.Drawing.Icon("logo.ico");
	        trayicon.Visible = false; //Hide unless user minimizes program
	        trayicon.Click +=
	            delegate(object sender, EventArgs args)
	            {
	                this.Show();
	                trayicon.Visible = false;
	                this.WindowState = WindowState.Normal;
	            };
	        
	        red_candle = new SolidColorBrush(System.Windows.Media.Color.FromRgb(234,0,112));
	        green_candle = new SolidColorBrush(System.Windows.Media.Color.FromRgb(175,255,49));
	        darkgreen_candle = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99,171,29));
	        dark_ui_panel = new SolidColorBrush(System.Windows.Media.Color.FromRgb(9,11,13));
	        dark_ui_foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(153,153,153));

	        if(App.my_wallet_pass.Length > 0){
	        	Wallet_Enc_Title.Header = "Decrypt Wallet";
	        }
	        
	        this.Title = "NebliDex: A Decentralized Neblio Exchange "+App.version_text;
	        default_ui_gradient = (LinearGradientBrush)Wallet_Panel_View.Background; //Get the default linear panel brush
			default_marketbox_style = Market_Box.Style;
			default_canvas_border = (SolidColorBrush)Chart_Canvas_Border_View.Background;
			default_canvas_color = (SolidColorBrush)Chart_Canvas.Background;
			
			current_ui_look = App.default_ui_look;
		}
		
		//Used to autosize the tradelists
		private void TradeListView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
		    System.Windows.Controls.ListView listView = sender as System.Windows.Controls.ListView;
		    GridView gView = listView.View as GridView;
		
		    var workingWidth = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar
		    var totalcolumns = gView.Columns.Count; //The total amount of columns
		    var multi = 1.0/Convert.ToDouble(totalcolumns);
		    int i = 0;
		    for(i = 0;i < totalcolumns;i++){
		    	gView.Columns[i].Width = workingWidth*multi;
		    }
		    
		    if(listView == Wallet_List){
			    for(i = 0;i < totalcolumns;i++){
		    		if(i==1){
		    			//The amount
		    			gView.Columns[i].Width = workingWidth*0.4;
		    		}else{
		    			gView.Columns[i].Width = workingWidth*0.3;
		    		}
			    }
		    }
		}
		
		private void Chart_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			//Resize the Candle Width when the size changes
			if(Chart_Canvas.ActualWidth <= 0){return;}
			Canvas.SetLeft(Chart_Mouse_Price,Chart_Canvas.ActualWidth/2-Chart_Mouse_Price.ActualWidth/2);
			
			//System.Diagnostics.Debug.WriteLine(Chart_Canvas.w);
			AdjustCandlePositions();

		}
		
		private void Chart_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			//This function is executed when mouse moved over chart
			if(last_candle_time <= 0){return;}
			if(Chart_Canvas.ActualWidth <= 0){return;}
			System.Windows.Point p = e.GetPosition(Chart_Canvas);
			int old_candle_time = 0;
			if(chart_timeline == 0){
				old_candle_time = last_candle_time - 60*60*24;
			}else{
				old_candle_time = last_candle_time - 60*60*24*7;
			}
			double gridwidth = Chart_Canvas.ActualWidth / 100.0;
			int grid = (int)Math.Floor(p.X / gridwidth);
			
			gridwidth = (last_candle_time-old_candle_time) / 100.0;
			int gridtime = old_candle_time+(int)Math.Round(grid*gridwidth);
			
			if(chart_high == chart_low){return;}
			double ratio = (chart_high-chart_low) / Chart_Canvas.ActualHeight;
			double price = Math.Round(chart_low + (Chart_Canvas.ActualHeight-p.Y)*ratio,8);
			Chart_Mouse_Price.Visibility = Visibility.Visible; //Show the Text
			if(price < 0){price = 0; Chart_Mouse_Price.Visibility = Visibility.Hidden;}
			
			Chart_Mouse_Price.Content = App.UTC2DateTime(gridtime).ToString("yyyy-MM-dd HH:mm")+" | "+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",price);
		}
		
		private void Select_Order(object sender, MouseButtonEventArgs e)
		{
			if(App.critical_node == true){
				System.Windows.MessageBox.Show("Cannot Match An Order in Critical Node Mode");
				return;
			}
			if(Convert.ToString(Market_Percent.Content) == "LOADING..."){return;}
			
		    var item = sender as System.Windows.Controls.ListView;
		    if(item == null){return;}
		    if(item.SelectedItem == null){return;}
		    App.OpenOrder ord  = (App.OpenOrder)item.SelectedItem; //This is order
		    
		    //Verify that order is not our own order
		    bool notmine=true;
		    lock(App.MyOpenOrderList){
		    	for(int i = 0;i < App.MyOpenOrderList.Count;i++){
		    		if(App.MyOpenOrderList[i].order_nonce == ord.order_nonce){
		    			notmine = false;break;
		    		}
		    	}
		    }
		    
		    if(notmine == true){
			    MatchOrder m_dialog = new MatchOrder(ord);
			    m_dialog.ShowDialog();		    	
		    }else{
		    	System.Windows.MessageBox.Show("Cannot match with your own order!");
		    }

		}
		
		public int Selling_View_Timer=0;
		public int Buying_View_Timer=0;
		
		private void Reset_AutoScroll(object sender, MouseButtonEventArgs e)
		{
			//Everytime the list is touched, it doesn't update on new order for 5 seconds
			var my_list = sender as System.Windows.Controls.ListView;
			if(my_list == Selling_View){
				Selling_View_Timer = App.UTCTime();
			}else if(my_list == Buying_View){
				Buying_View_Timer = App.UTCTime();
			}
		}

		public void LoadUI()
		{
			//This function will load the UI based on the data present
			CreateMarketsUI();
			
			//Change the Sell and Buy labels
			Buy_Button.Content = "Buy "+App.MarketList[App.exchange_market].trade_symbol;
			Sell_Button.Content = "Sell "+App.MarketList[App.exchange_market].trade_symbol;
			
			//First Load Wallets
			Wallet_List.ItemsSource = App.WalletList;
			
			//Then Trade history
			SQLiteConnection mycon = new SQLiteConnection("Data Source=\""+App.App_Path+"/data/neblidex.db\";Version=3;");
			mycon.Open();
			
			//Set our busy timeout, so we wait if there are locks present
			SQLiteCommand statement = new SQLiteCommand("PRAGMA busy_timeout = 5000",mycon); //Create a transaction to make inserts faster
			statement.ExecuteNonQuery();
			statement.Dispose();
				
			//Select all the rows from tradehistory
			string myquery = "Select utctime, market, type, price, amount, pending, txhash From MYTRADEHISTORY Order By utctime DESC";
			statement = new SQLiteCommand(myquery,mycon);
			SQLiteDataReader statement_reader = statement.ExecuteReader();
			while(statement_reader.Read()){
				string format_date="";
				string format_type;
				string format_market="";
				string txhash = statement_reader["txhash"].ToString();
				int utctime = Convert.ToInt32(statement_reader["utctime"]);
				if(Convert.ToInt32(statement_reader["pending"]) == 0){
					format_date = App.UTC2DateTime(utctime).ToString("yyyy-MM-dd");
				}else if(Convert.ToInt32(statement_reader["pending"]) == 1){
					format_date = "PENDING";
				}else{
					format_date = "CANCELLED";
				}
				if(Convert.ToInt32(statement_reader["type"]) == 0){
					format_type = "BUY";
				}else{
					format_type = "SELL";
				}
				int market = Convert.ToInt32(statement_reader["market"]);
				decimal price = Decimal.Parse(statement_reader["price"].ToString(),NumberStyles.Float,CultureInfo.InvariantCulture);
				decimal amount = Decimal.Parse(statement_reader["amount"].ToString(),NumberStyles.Float,CultureInfo.InvariantCulture);
				format_market = App.MarketList[market].format_market;
				Trade_History_List.Items.Add(new App.MyTrade{ Date = format_date, Pair = format_market, Type = format_type, Price = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",price), Amount = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",amount), TxID = txhash  } );
			}
			statement_reader.Close();
			statement.Dispose();
			
			//Load from the CN fees table to the chart
			//Select all the rows from tradehistory
			myquery = "Select utctime, market, fee From CNFEES Order By utctime DESC";
			statement = new SQLiteCommand(myquery,mycon);
			statement_reader = statement.ExecuteReader();
			while(statement_reader.Read()){
				string format_date="";
				string format_market="";
				int utctime = Convert.ToInt32(statement_reader["utctime"]);
				format_date = App.UTC2DateTime(utctime).ToString("yyyy-MM-dd");
				int market = Convert.ToInt32(statement_reader["market"]);
				decimal fee = Decimal.Parse(statement_reader["fee"].ToString(),NumberStyles.Float,CultureInfo.InvariantCulture);
				format_market = App.MarketList[market].format_market;
				CN_Tx_List.Items.Add(new { Date = format_date, Pair = format_market, Fee = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",fee) } );
			}
			statement_reader.Close();
			statement.Dispose();
			
			//Now load the Candle information for the visible chart (NDEX/NEBL is default market & 24 HR is default timeline) from SQLite DB (added during sync period)
			//The server will send how many seconds left on the most recent candle (for both times), before moving forward
			int backtime = App.UTCTime() - 60*60*25;
			myquery = "Select highprice, lowprice, open, close From CANDLESTICKS24H Where market = @mark And utctime > @time Order By utctime ASC"; //Show results from oldest to most recent
			statement = new SQLiteCommand(myquery,mycon);
			statement.Parameters.AddWithValue("@time",backtime);
			statement.Parameters.AddWithValue("@mark",App.exchange_market);
			statement_reader = statement.ExecuteReader();
			while(statement_reader.Read()){
				//Go Candle by Candle to get results
				App.Candle can = new App.Candle();
				//Must use cultureinfo as some countries see . as ,
				can.open = Convert.ToDouble(statement_reader["open"],CultureInfo.InvariantCulture);
				can.close = Convert.ToDouble(statement_reader["close"],CultureInfo.InvariantCulture);
				can.low = Convert.ToDouble(statement_reader["lowprice"],CultureInfo.InvariantCulture);
				can.high = Convert.ToDouble(statement_reader["highprice"],CultureInfo.InvariantCulture);
				App.AddCandleShapes(can);
				PlaceCandleInChart(can);
			}
			statement_reader.Close();
			statement.Dispose();
			mycon.Close();
			
			//Add a recent candle based on the last trade
			lock(App.ChartLastPrice){
				AddCurrentCandle();
			}
			
			Recent_Trade_List.ItemsSource = App.RecentTradeList[App.exchange_market]; //Make this view match the list

			//This List will be pre-sorted for highest price to lowest
			//These are auto sorts
			Selling_View.Items.SortDescriptions.Add(new SortDescription("Format_Price_Double", ListSortDirection.Descending));
			Buying_View.Items.SortDescriptions.Add(new SortDescription("Format_Price_Double", ListSortDirection.Descending));
					
			//Populate the sell list first
			for(int i=0;i < App.OpenOrderList[App.exchange_market].Count;i++){
				if(App.OpenOrderList[App.exchange_market][i].type == 1){
					//Sell Orders
					Selling_View.Items.Add(App.OpenOrderList[App.exchange_market][i]);
				}
			}
			
			//And buy list then
			for(int i=0;i < App.OpenOrderList[App.exchange_market].Count;i++){
				if(App.OpenOrderList[App.exchange_market][i].type == 0){
					//Buy Orders
					Buying_View.Items.Add(App.OpenOrderList[App.exchange_market][i]);
				}
			}
			
			//Sort the views
			Selling_View.Items.Refresh();
			Buying_View.Items.Refresh();
			
			//Scroll Automatically to Bottom for Sell List
			if(Selling_View.Items.Count > 0){
				Selling_View.ScrollIntoView(Selling_View.Items[Selling_View.Items.Count-1]);
			}

			//My Open Items list
			Open_Orders_List.ItemsSource = App.MyOpenOrderList;
			
			//Show the initial fees as well
			UpdateBlockrates();
			
		}
		
		public void UpdateBlockrates()
		{
			//Make sure all the Dex connections exists
			bool not_connected = false;
			//contype 1 now represents all electrum connections but different cointypes
			lock(App.DexConnectionList){
				bool connnection_exist;
				for(int cit = 1;cit < App.total_cointypes;cit++){
					//Go through all the blockchain types and make sure an electrum connection exists for it, skip Neblio blockchain as it doesn't use electrum
					if(cit == 6){continue;} //Etheruem doesn't use dexconnection
					connnection_exist = false;
					for(int i = 0;i < App.DexConnectionList.Count;i++){
						if(App.DexConnectionList[i].open == true && App.DexConnectionList[i].contype == 1 && App.DexConnectionList[i].blockchain_type == cit){
							connnection_exist = true;
							break;
						}
					}
					if(connnection_exist == false){
						not_connected = true;
						break;
					}
				}
				//Now detect if client is connected to a CN node
				if(App.critical_node == false){
					connnection_exist = false;
					for(int i = 0;i < App.DexConnectionList.Count;i++){
						if(App.DexConnectionList[i].open == true && App.DexConnectionList[i].contype == 3){
							connnection_exist = true;
							break;
						}
					}
					if(connnection_exist == false){
						not_connected = true;
					}
				}
			}
			
			if(not_connected == false && App.ntp1downcounter < 2){
				//Update the block rate status bar based on the market
				Fee_Status.Content = "Current Blockchain Fees:";
				if(App.using_blockhelper == true){
					Fee_Status.Content = "BlockHelper Active | "+Fee_Status.Content;
				}
				Fee_Status.Foreground = System.Windows.Media.Brushes.Black;
				CN_Fee.Content = "CN Fee: "+App.ndex_fee;
				int trade_wallet_blockchaintype = App.GetWalletBlockchainType(App.MarketList[App.exchange_market].trade_wallet);
				int base_wallet_blockchaintype = App.GetWalletBlockchainType(App.MarketList[App.exchange_market].base_wallet);
				
				//Update Status Bar Fees
				if(trade_wallet_blockchaintype != 0){
					if(trade_wallet_blockchaintype == 6){
						NEBL_Fee.Content = App.MarketList[App.exchange_market].trade_symbol+" Fee: "+String.Format(CultureInfo.InvariantCulture,"{0:0.##}",Math.Round(App.blockchain_fee[trade_wallet_blockchaintype],2))+" Gwei";
					}else{
						NEBL_Fee.Content = App.MarketList[App.exchange_market].trade_symbol+" Fee: "+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",Math.Round(App.blockchain_fee[trade_wallet_blockchaintype],8))+"/kb";
					}
				}else{
					NEBL_Fee.Content = "NEBL Fee: "+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",Math.Round(App.blockchain_fee[trade_wallet_blockchaintype],8))+"/kb";
				}
				if(trade_wallet_blockchaintype != base_wallet_blockchaintype){
					//Show both the trade and base fees
					Base_Pair_Separator.Visibility = Visibility.Visible;
					Base_Pair_Fee.Content = App.MarketList[App.exchange_market].base_symbol+" Fee: "+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",Math.Round(App.blockchain_fee[base_wallet_blockchaintype],8))+"/kb";
				}else{
					//Only show the trade fee as they use the same blockchaintype
					Base_Pair_Fee.Content = "";
					Base_Pair_Separator.Visibility = Visibility.Collapsed;	
				}
				
				if(App.critical_node == true){
					int cn_online = App.CN_Nodes_By_IP.Count;
					string percent = String.Format(CultureInfo.InvariantCulture,"{0:0.###}",Math.Round(App.my_cn_weight*100,3));
					CN_Info.Content = "Tx Validating: "+App.cn_num_validating_tx+" (CNs Online: "+cn_online+", "+percent+"% Chance of Validating)";
				}else{
					CN_Info.Content = "";
				}
			}else{
				Fee_Status.Content = "Not Fully Connected:";
				Fee_Status.Foreground = System.Windows.Media.Brushes.Red;
			}
			
		}
		
		public void CreateMarketsUI()
		{

			//Update combo box to show markets
			for(int i=0;i < App.MarketList.Count;i++){
				if(App.MarketList[i].active == false){
					continue;
				}
				string format_market = App.MarketList[i].format_market;
				//We are going to alphabetically sort the marketlist
				bool not_found = true;
				int pos = 0;
				for(int i2 = 0;i2 < Market_Box.Items.Count;i2++){
					string item_detail = (string)Market_Box.Items[i2];
					int compare = String.Compare(format_market,item_detail,true);
					if(compare < 0){
						not_found = false;
						//Format Market precedes item_detail, add it in front
						Market_Box.Items.Insert(i2,App.MarketList[i].format_market);
						pos = i2;
						break;
					}
				}
				if(not_found == true){
					Market_Box.Items.Add(App.MarketList[i].format_market);
					pos = Market_Box.Items.Count-1;
				}
				if(i == App.exchange_market){
					//Select this by default
					Market_Box.SelectedIndex = pos;
				}
			}			
		}
		
		public void RefreshUI()
		{
			//This will reload the visuals on all the lists and charts for a new market
			//0 - NEBL/BTC
			//1 - NEBL/LTC
			//2 - NDEX/NEBL
			
			//Update the buttons
			Buy_Button.Content = "Buy "+App.MarketList[App.exchange_market].trade_symbol;
			Sell_Button.Content = "Sell "+App.MarketList[App.exchange_market].trade_symbol;
			
			//Clear the Order List for the market and reload for the new market
			Selling_View.Items.Clear();
			Buying_View.Items.Clear();
			
			//Repopulate list
			for(int i=0;i < App.OpenOrderList[App.exchange_market].Count;i++){
				if(App.OpenOrderList[App.exchange_market][i].type == 1){
					//Sell Orders
					Selling_View.Items.Add(App.OpenOrderList[App.exchange_market][i]);
				}
			}
			
			//And buy list then
			for(int i=0;i < App.OpenOrderList[App.exchange_market].Count;i++){
				if(App.OpenOrderList[App.exchange_market][i].type == 0){
					//Buy Orders
					Buying_View.Items.Add(App.OpenOrderList[App.exchange_market][i]);
				}
			}
			//Re-position Lists
			if(Selling_View.Items.Count > 0){
				Selling_View.ScrollIntoView(Selling_View.Items[Selling_View.Items.Count-1]);
			}
			Selling_View.Items.Refresh();
			if(Buying_View.Items.Count > 0){
				Buying_View.ScrollIntoView(Buying_View.Items[0]);
			}
			Buying_View.Items.Refresh();
			
			//Change the recent trade list
			Recent_Trade_List.ItemsSource = App.RecentTradeList[App.exchange_market];
			Recent_Trade_List.Items.Refresh();
			
			//Update the Candles as well
			lock(App.ChartLastPrice){
				UpdateCandles();
			}
			
			//Update block rates
			UpdateBlockrates();
					
		}
		
		public void AddOrderToView(App.OpenOrder ord)
		{
			//This function adds an order the view
			if(ord.market != App.exchange_market){return;} //Not on this market, do not add to view
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					//Must be on UI thread
					if(ord.type == 0){
						//Buying view
						lock(Buying_View.Items){
							Buying_View.Items.Add(ord);
							Buying_View.Items.Refresh(); //Should sort automatically
						}
						if(App.UTCTime()-Buying_View_Timer > 5){
							//Auto scroll
							if(Buying_View.Items.Count > 0){
								Buying_View.ScrollIntoView(Buying_View.Items[0]);
							}
						}
					}else if(ord.type == 1){
						lock(Selling_View.Items){
							Selling_View.Items.Add(ord);
							Selling_View.Items.Refresh(); //Should sort automatically
						}
						if(App.UTCTime()-Selling_View_Timer > 5){
							//Auto scroll
							if(Selling_View.Items.Count > 0){
								Selling_View.ScrollIntoView(Selling_View.Items[Selling_View.Items.Count-1]);
							}
						}						
					}
				}));
		}
		
		public void RemoveOrderFromView(App.OpenOrder ord)
		{
			//This function adds an order the view
			if(ord.market != App.exchange_market){return;} //Not on this market, do not need to remove to view
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					//Must be on UI thread
					if(ord.type == 0){
						//Buying view
						lock(Buying_View.Items){
							for(int i = Buying_View.Items.Count-1;i >= 0;i--){
								App.OpenOrder ord2 = (App.OpenOrder)Buying_View.Items[i];
								if(ord2.order_nonce.Equals(ord.order_nonce) == true){ //Remove matching nonce
									Buying_View.Items.RemoveAt(i);
								}
							}
							Buying_View.Items.Refresh(); //Should sort automatically
						}
					}else if(ord.type == 1){
						lock(Selling_View.Items){
							for(int i = Selling_View.Items.Count-1;i >= 0;i--){
								App.OpenOrder ord2 = (App.OpenOrder)Selling_View.Items[i];
								if(ord2.order_nonce.Equals(ord.order_nonce) == true){
									Selling_View.Items.RemoveAt(i);
								}
							}
							Selling_View.Items.Refresh(); //Should sort automatically
						}					
					}
				}));
		}
		
		public static void PendOrder(string nonce)
		{
			//This is a CN to CN or to TN function
			App.OpenOrder ord = null;
			for(int market = 0;market < App.total_markets;market++){
				lock(App.OpenOrderList[market]){
					for(int i = 0;i < App.OpenOrderList[market].Count;i++){
						if(App.OpenOrderList[market][i].order_nonce.Equals(nonce) == true){
							if(App.OpenOrderList[market][i].order_stage > 0){break;} //Shouldn't happen normally
							App.OpenOrderList[market][i].pendtime = App.UTCTime(); //This pended order will remove itself in 3 hours if still pending
							App.OpenOrderList[market][i].order_stage = 1; //Pending
							ord = App.OpenOrderList[market][i];
							break;
						}
					}
				}
			}
			
			if(ord == null){return;}
			if(App.main_window_loaded == false){return;}
			if(App.exchange_market != ord.market){return;}
			
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					Window1 my_win = App.main_window;
					//Must be on UI thread
					if(ord.type == 0){
						//Buying view
						my_win.Buying_View.Items.Refresh();
					}else if(ord.type == 1){
						my_win.Selling_View.Items.Refresh();						
					}
				}));
		}
		
		public static bool ShowOrder(string nonce)
		{
			//This is a CN to CN and to TN function
			App.OpenOrder ord = null;
			for(int market = 0;market < App.total_markets;market++){
				lock(App.OpenOrderList[market]){
					for(int i = 0;i < App.OpenOrderList[market].Count;i++){
						if(App.OpenOrderList[market][i].order_nonce.Equals(nonce) == true){
							if(App.OpenOrderList[market][i].my_order == true){
								//This order nonce belongs to me
								if(App.OpenOrderList[market][i].order_stage > 0){break;} //Shouldn't happen normally
							}
							App.OpenOrderList[market][i].order_stage = 0; //Show order
							ord = App.OpenOrderList[market][i];
							break;
						}
					}
				}
			}
			
			if(ord == null){return false;}
			if(App.main_window_loaded == false){return false;}
			if(App.exchange_market != ord.market){return true;} //Still valid, just not showing the market
			
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					Window1 my_win = App.main_window;
					//Must be on UI thread
					if(ord.type == 0){
						//Buying view
						my_win.Buying_View.Items.Refresh();
					}else if(ord.type == 1){
						my_win.Selling_View.Items.Refresh();						
					}
				}));
			return true;
		}
		
		public void showTradeMessage(string msg)
		{
			if(msg.Length > 200){return;} //Really long message, do not show
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					//Must be on UI thread
					this.Focus();
					System.Windows.MessageBox.Show(msg,"NebliDex: Trade Notice!");
				}));
		}
		
		public void UpdateCandles()
		{
			//Clear the Visible Candles and reload the charts for the appropriate timescale and market
			for(int i = 0;i < App.VisibleCandles.Count;i++){
				Chart_Canvas.Children.Remove(App.VisibleCandles[i].rect);
				Chart_Canvas.Children.Remove(App.VisibleCandles[i].line);
			}
			App.VisibleCandles.Clear();
			
			Market_Percent.Foreground = System.Windows.Media.Brushes.White;
			if(current_ui_look == 1){
				//Light theme
				Market_Percent.Foreground = System.Windows.Media.Brushes.Black;
			}else if(current_ui_look == 2){
				Market_Percent.Foreground = dark_ui_foreground;
			}
			Market_Percent.Content = "00.00%";
			Chart_Last_Price.Content = "Last Price:";
			
			SQLiteConnection mycon = new SQLiteConnection("Data Source=\""+App.App_Path+"/data/neblidex.db\";Version=3;");
			mycon.Open();
			
			//Set our busy timeout, so we wait if there are locks present
			SQLiteCommand statement = new SQLiteCommand("PRAGMA busy_timeout = 5000",mycon); //Create a transaction to make inserts faster
			statement.ExecuteNonQuery();
			statement.Dispose();
				
			string myquery="";
			int backtime = 0;
			
			if(chart_timeline == 0){ //24 hr
				backtime = App.UTCTime() - 60*60*25;
				myquery = "Select highprice, lowprice, open, close From CANDLESTICKS24H Where market = @mark And utctime > @time Order By utctime ASC";				
			}else if(chart_timeline == 1){ //7 day
				backtime = App.UTCTime() - (int)Math.Round(60.0*60.0*24.0*6.25); //Closer to actual time of 100 candles
				myquery = "Select highprice, lowprice, open, close From CANDLESTICKS7D Where market = @mark And utctime > @time Order By utctime ASC";				
			}

			statement = new SQLiteCommand(myquery,mycon);
			statement.Parameters.AddWithValue("@time",backtime);
			statement.Parameters.AddWithValue("@mark",App.exchange_market);
			SQLiteDataReader statement_reader = statement.ExecuteReader();
			while(statement_reader.Read()){
				//Go Candle by Candle to get results
				App.Candle can = new App.Candle();
				//Must use cultureinfo as some countries see . as ,
				can.open = Convert.ToDouble(statement_reader["open"],CultureInfo.InvariantCulture);
				can.close = Convert.ToDouble(statement_reader["close"],CultureInfo.InvariantCulture);
				can.low = Convert.ToDouble(statement_reader["lowprice"],CultureInfo.InvariantCulture);
				can.high = Convert.ToDouble(statement_reader["highprice"],CultureInfo.InvariantCulture);
				App.AddCandleShapes(can);
				PlaceCandleInChart(can);
			}
			statement_reader.Close();
			statement.Dispose();
			
			mycon.Close();
			
			AddCurrentCandle();
		}
		
		public void AddCurrentCandle()
		{
			if(App.ChartLastPrice[chart_timeline].Count == 0){return;}
			//This will add a new candle based on current last chart prices
			//Then Load the current candle into the chart. This candle is not stored in database and based soley and chartlastprice
			double open=-1,close=-1,high=-1,low=-1;
			for(int pos = 0;pos < App.ChartLastPrice[chart_timeline].Count;pos++){
				if(App.ChartLastPrice[chart_timeline][pos].market == App.exchange_market){
					double price = Convert.ToDouble( App.ChartLastPrice[chart_timeline][pos].price);
					if(open < 0){open = price;}
					if(price > high){
						high = price;
					}
					if(low < 0 || price < low){
						low = price;
					}
					close = price; //The last price will be the close
				}					
			}
			if(open > 0){
				//May not have any candles for this market
				App.Candle new_can = new App.Candle();
				new_can.open = open;
				new_can.close = close;
				new_can.high = high;
				new_can.low = low;
				App.AddCandleShapes(new_can);
				PlaceCandleInChart(new_can);
			}
		}
		
		public void PlaceCandleInChart(App.Candle can)
		{
			//First adjust the candle high and low if the open and close are the same
			if(can.high == can.low){
				can.high = can.high+App.double_epsilon; //Allow us to create a range
				can.low = can.low-App.double_epsilon;
				if(can.low < 0){can.low = 0;}
			}
			
			//And it will add it to the list
			if(App.VisibleCandles.Count >= 100){
				Chart_Canvas.Children.Remove(App.VisibleCandles[0].line); //Remove from Canvas first
				Chart_Canvas.Children.Remove(App.VisibleCandles[0].rect);
				App.VisibleCandles.RemoveAt(0); //Remove the first / oldest candle
			}
			App.VisibleCandles.Add(can);
			Chart_Canvas.Children.Add(can.line);
			Chart_Canvas.Children.Add(can.rect);
			Canvas.SetZIndex(can.rect,0);
			Canvas.SetZIndex(can.line,0);
			AdjustCandlePositions();
			last_candle_time = App.UTCTime();
		}
		
		public void UpdateLastCandle(double val)
		{
			if(App.VisibleCandles.Count == 0){
				//Make a new candle, how history exists
				App.Candle can = new App.Candle();
				can.open = val;
				can.close = val;
				can.high = val;
				can.low = val;;
				App.AddCandleShapes(can);
				PlaceCandleInChart(can);
			}else{
				//This will update the value for the last candle
				App.Candle can = App.VisibleCandles[App.VisibleCandles.Count-1]; //Get last candle
				if(val > can.high){
					can.high = val;
				}
				if(val < can.low){
					can.low = val;
				}
				
				can.close = val;
				
				//Look at the last chartlastprice to find the close price for the candle
				int timeline = chart_timeline;
				for(int i = App.ChartLastPrice[timeline].Count-1; i >= 0;i--){
					if(App.ChartLastPrice[timeline][i].market == App.exchange_market){
						can.close = Convert.ToDouble(App.ChartLastPrice[timeline][i].price);
						break;
					}
				}
				
				if(can.close >= can.open){
					//Positive
					can.rect.Fill = green_candle;
					if(current_ui_look == 1){
						//White
						can.rect.Fill = darkgreen_candle;
					}
				}else{
					can.rect.Fill = red_candle;
				}
			}
			AdjustCandlePositions();
		}
		
		public static void UpdateOpenOrderList(int market, string order_nonce)
		{
			//This function will remove 0 sized orders
			//First find the open order from our list
			App.OpenOrder ord=null;
			lock(App.OpenOrderList[market]){
				for(int i = App.OpenOrderList[market].Count-1;i >= 0;i--){
					if(App.OpenOrderList[market][i].order_nonce == order_nonce && App.OpenOrderList[market][i].is_request == false){
						ord = App.OpenOrderList[market][i];
						if(App.OpenOrderList[market][i].amount <= 0){ //Take off the order if its empty now							
							App.OpenOrderList[market].RemoveAt(i);break;
						}
					}
				}
			}
			
			if(ord == null){return;}
			
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					if(market == App.exchange_market && App.main_window_loaded == true){
						if(ord != null){
							if(ord.type == 0){
								//Buying view
								if(ord.amount <= 0){
									App.main_window.Buying_View.Items.Remove(ord);
								}
								App.main_window.Buying_View.Items.Refresh();
							}else if(ord.type == 1){
								if(ord.amount <= 0){
									App.main_window.Selling_View.Items.Remove(ord);
								}
								App.main_window.Selling_View.Items.Refresh();						
							}							
						}
					}
				}));
		}
		
		public static void AddRecentTradeToView(int market,int type,decimal price, decimal amount, string order_nonce,int time)
		{
			
			if(amount <= 0){return;} //Someone is trying to hack the system
			
			//First check if our open orders matches this recent trade
			App.OpenOrder myord = null;
			lock(App.MyOpenOrderList){
				for(int i=App.MyOpenOrderList.Count-1;i >= 0;i--){
					if(App.MyOpenOrderList[i].order_nonce == order_nonce){
						if(App.MyOpenOrderList[i].is_request == false){
							myord = App.MyOpenOrderList[i];
							break;
						}else{
							//I am requesting this order, should only occur during simultaneous order request
							//when only one of the orders match. Clear this request and tell user someone else took order
							App.MyOpenOrderList.RemoveAt(i);
							if(App.main_window_loaded == true){
								App.main_window.showTradeMessage("Trade Failed:\nSomeone else matched this order before you!");
								//Update the view
								System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
									() =>
									{
										App.main_window.Open_Orders_List.Items.Refresh();
									}));
							}
						}
					}
				}
			}
			
			if(App.critical_node == false){
				if(market != App.exchange_market){return;} //We don't care about recent trades from other markets if not critical node
			}
			
			//Also modify the open order that is not our order
			lock(App.OpenOrderList[market]){
				for(int i = App.OpenOrderList[market].Count-1;i >= 0;i--){
					if(App.OpenOrderList[market][i].order_nonce == order_nonce && App.OpenOrderList[market][i].is_request == false){
						if(App.OpenOrderList[market][i] != myord){
							//Maker will decrease its own balance separately
							App.OpenOrderList[market][i].amount -= amount; //We already subtracted the amount if my order
							App.OpenOrderList[market][i].amount = Math.Round(App.OpenOrderList[market][i].amount,8);
						}
					}
				}
			}
			
			//This will also calculate the chartlastprice and modify the candle
			
			App.RecentTrade rt = new App.RecentTrade();
			rt.amount = amount;
			rt.market = market;
			rt.price = price;
			rt.type = type;
			rt.utctime = time;
			
			InsertRecentTradeByTime(rt); //Insert the trade by time
			
			//First check to see if this time has already passed the current candle
			bool[] updatedcandle = new bool[2];
			if(time < App.ChartLastPrice15StartTime){
				//This time is prior to the start of the candle
				updatedcandle[0] = TryUpdateOldCandle(market,Convert.ToDouble(price),time,0);
				if(updatedcandle[0] == true){
					App.NebliDexNetLog("Updated a previous 15 minute candle");
				}
				updatedcandle[1] = TryUpdateOldCandle(market,Convert.ToDouble(price),time,1);
				if(updatedcandle[1] == true){
					App.NebliDexNetLog("Updated a previous 90 minute candle");
				}
			}
			
			App.LastPriceObject pr = new App.LastPriceObject();
			pr.price = price;
			pr.market = market;
			pr.atime = time;
			lock(App.ChartLastPrice){ //Adding prices is not thread safe
				if(updatedcandle[0] == false){
					InsertChartLastPriceByTime(App.ChartLastPrice[0],pr);
				}
				
				if(updatedcandle[1] == false){
					InsertChartLastPriceByTime(App.ChartLastPrice[1],pr);
				}
			}
			
			//Update the current candle
			System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
				() =>
				{
					if(market == App.exchange_market && App.main_window_loaded == true){
						Window1 my_win = App.main_window;
						if(updatedcandle[my_win.chart_timeline] == false){
							//This most recent candle hasn't been updated yet
							lock(App.ChartLastPrice){
								my_win.UpdateLastCandle(Convert.ToDouble(price));
							}
						}else{
							//Just refresh the view
							lock(App.ChartLastPrice){
								my_win.UpdateCandles();
							}
						}
						my_win.Recent_Trade_List.Items.Refresh();
					}
				}));
			Window1.UpdateOpenOrderList(market,order_nonce); //This will update the views if necessary and remove the order
		}
		
		public static void InsertChartLastPriceByTime(List<App.LastPriceObject> mylist,App.LastPriceObject lp)
		{
			//The most recent chartlastprice is in the end
			bool inserted = false;
			for(int i = mylist.Count-1;i >= 0;i--){
				//Insert based on first the time
				App.LastPriceObject plp = mylist[i];
				if(plp.market == lp.market){
					//Must be same market
					if(plp.atime < lp.atime){
						//Place the last chart time here
						mylist.Insert(i+1,lp);
						inserted = true;
						break;
					}else if(plp.atime == lp.atime){
						//These trades were made at the same time
						//Compare the prices
						if(plp.price > lp.price){
							mylist.Insert(i+1,lp);
							inserted = true;
							break;
						}
					}
				}
			}
			if(inserted == false){
				mylist.Insert(0,lp); //Add to beginning of the list
			}			
		}
		
		public static void InsertRecentTradeByTime(App.RecentTrade rt)
		{
			//This will insert the recent trade into the list by time (higher first), and then price (lower first)
			//Most recent trade is first on list
			lock(App.RecentTradeList[rt.market]){
				bool inserted = false;
				for(int i = 0;i < App.RecentTradeList[rt.market].Count;i++){
					//Insert based on first the time
					App.RecentTrade prt = App.RecentTradeList[rt.market][i];
					if(prt.utctime < rt.utctime){
						//Place the recent trade here
						App.RecentTradeList[rt.market].Insert(i,rt);
						inserted = true;
						break;
					}else if(prt.utctime == rt.utctime){
						//These trades were made at the same time
						//Compare the prices
						if(prt.price > rt.price){
							App.RecentTradeList[rt.market].Insert(i,rt);
							inserted = true;
							break;
						}
					}
				}
				if(inserted == false){
					App.RecentTradeList[rt.market].Add(rt); //Add to end of list, old trade
				}
			}
		}
			
		
		public void AdjustCandlePositions()
		{
			//This function will update the canvas and graphs to make candles appear well
			if(Double.IsInfinity(Chart_Canvas.ActualHeight)){return;}
			if(Double.IsNaN(Chart_Canvas.ActualHeight)){return;}
			if(Chart_Canvas.ActualHeight <= 0){return;}
			if(App.VisibleCandles.Count == 0){return;}
			
			double lowest = -1;
			double highest = -1;
			for(int i=0;i < App.VisibleCandles.Count;i++){
				//Go through each candle to find maximum height and maximum low
				if(App.VisibleCandles[i].low < lowest || lowest < 0){
					lowest = App.VisibleCandles[i].low;
				}
				if(App.VisibleCandles[i].high > highest || highest < 0){
					highest = App.VisibleCandles[i].high;
				}
			}
			
			double middle = (highest - lowest) / 2.0; //Should be the middle of the chart
			if(middle <= 0){return;} //Shouldn't happen unless flat market
			
			//Make it so that the candles don't hit buttons
			lowest = lowest - (highest - lowest)*0.1;
			highest = highest + (highest - lowest)*0.1;
			
			//Calculate Scales
			double ChartScale = Chart_Canvas.ActualHeight/(highest - lowest);
			double width = Chart_Canvas.ActualWidth/100.0;
			
			//Position Candles based on scale and width
			//Total of 100 candles visible so each candle needs to be 1/100 of chart
			double xpos = 0;
			double ypos = 0;
			double candles_width = App.VisibleCandles.Count*width; //The width of the entire set of candles
			double height=0;
			for(int i=0;i < App.VisibleCandles.Count;i++){
				xpos = (Chart_Canvas.ActualWidth - candles_width); //Start position
				xpos = xpos + i*width; //Current Position
				
				App.VisibleCandles[i].rect.Width = width; //Set the Width
				Canvas.SetLeft(App.VisibleCandles[i].rect,xpos); //Set the X position of Rect
				
				//Calculate height now
				if(App.VisibleCandles[i].open > App.VisibleCandles[i].close){
					//Red Candle
					height = (App.VisibleCandles[i].open - App.VisibleCandles[i].close)*ChartScale; //Calculate Height
					ypos = (App.VisibleCandles[i].close-lowest)*ChartScale; //Bottom Left Corner is 0,0
				}else{
					//Green candle
					height = (App.VisibleCandles[i].close - App.VisibleCandles[i].open)*ChartScale; //Calculate Height
					ypos = (App.VisibleCandles[i].open-lowest)*ChartScale; //Bottom Left Corner is 0,0
				}
				
				if(height < 1){height = 1;} //Show something
				App.VisibleCandles[i].rect.Height = height;
				Canvas.SetBottom(App.VisibleCandles[i].rect,ypos);
				
				//Calculate Outliers
				if(App.VisibleCandles[i].high - App.VisibleCandles[i].low >= App.double_epsilon*2.1){
					height = (App.VisibleCandles[i].high - App.VisibleCandles[i].low)*ChartScale;
					if(height < 1){height = 1;} //Show something
					ypos = (App.VisibleCandles[i].low - lowest)*ChartScale;
					App.VisibleCandles[i].line.Y1 = 0;
					App.VisibleCandles[i].line.Y2 = height;
					App.VisibleCandles[i].line.X1 = xpos+(width/2.0);
					App.VisibleCandles[i].line.X2 = xpos+(width/2.0);
					Canvas.SetBottom(App.VisibleCandles[i].line,ypos);
					App.VisibleCandles[i].line.Visibility = Visibility.Visible;
				}else{
					//Very small difference in low and high, essentially none
					App.VisibleCandles[i].line.Visibility = Visibility.Collapsed;
				}
				
			}
			
			chart_low = lowest;
			chart_high = highest;
			
			//Change the Market Percent
			double change = Math.Round((App.VisibleCandles[App.VisibleCandles.Count-1].close-App.VisibleCandles[0].open)/App.VisibleCandles[0].open*100,2);
			if(change == 0){
				Market_Percent.Foreground = System.Windows.Media.Brushes.White;
				if(current_ui_look == 1){
					//Light
					Market_Percent.Foreground = System.Windows.Media.Brushes.Black;
				}else if(current_ui_look == 2){
					Market_Percent.Foreground = dark_ui_foreground;
				}
				Market_Percent.Content = "00.00%";
			}else if(change > 0){
				//Green
				Market_Percent.Foreground = green_candle;
				if(current_ui_look == 1){
					Market_Percent.Foreground = darkgreen_candle;
				}
				if(change > 10000){
					Market_Percent.Content = "> +10000%";
				}else{
					Market_Percent.Content = "+"+change.ToString(CultureInfo.InvariantCulture)+"%";
				}
			}else if(change < 0){
				Market_Percent.Foreground = red_candle;
				if(change < -10000){
					Market_Percent.Content = "> -10000%";
				}else{
					Market_Percent.Content = ""+change.ToString(CultureInfo.InvariantCulture)+"%";
				}
			}
			Chart_Last_Price.Content = "Last Price: "+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",App.VisibleCandles[App.VisibleCandles.Count-1].close);
		}
		
		public static bool TryUpdateOldCandle(int market, double price, int time,int timescale)
		{
			//True means successfully updated the candle, no need to put in current candle
			
			//This will go through the database and update an old candle
			string myquery;
			SQLiteConnection mycon = new SQLiteConnection("Data Source=\""+App.App_Path+"/data/neblidex.db\";Version=3;");
			mycon.Open();
			
			SQLiteCommand statement = new SQLiteCommand("PRAGMA busy_timeout = 5000",mycon); //Create a transaction to make inserts faster
			statement.ExecuteNonQuery();
			statement.Dispose();
			
			string table;
			int timeforward = 0;
			if(timescale == 0){
				//24 hour chart
				table = "CANDLESTICKS24H";
				timeforward = 60*15;
			}else{
				table = "CANDLESTICKS7D";
				timeforward = 60*90;
			}

			//First update the most recent 24 hour candle
			int backtime = App.UTCTime() - 60*60*3;
			myquery = "Select highprice, lowprice, open, close, nindex, utctime From "+table+" Where market = @mark And utctime > @time Order By utctime DESC Limit 1"; //Get most recent candle
			statement = new SQLiteCommand(myquery,mycon);
			statement.Parameters.AddWithValue("@time",backtime);
			statement.Parameters.AddWithValue("@mark",market);
			SQLiteDataReader statement_reader = statement.ExecuteReader();
			bool dataavail = statement_reader.Read();
			double high,low,close,open;
			int nindex = -1;
			if(dataavail == true){
				high = Convert.ToDouble(statement_reader["highprice"].ToString(),CultureInfo.InvariantCulture);
				low = Convert.ToDouble(statement_reader["lowprice"].ToString(),CultureInfo.InvariantCulture);
				open = Convert.ToDouble(statement_reader["open"].ToString(),CultureInfo.InvariantCulture);
				close = Convert.ToDouble(statement_reader["close"].ToString(),CultureInfo.InvariantCulture);
				nindex = Convert.ToInt32(statement_reader["nindex"].ToString());
				int starttime = Convert.ToInt32(statement_reader["utctime"].ToString());
				statement_reader.Close();
				statement.Dispose();
				if(starttime+timeforward > time){
					//This candle needs to be updated
					if(price > high){
						high = price;
					}else if(price < low){
						low = price;
					}
					close = price;
					myquery = "Update "+table+" Set highprice = @hi, lowprice = @lo, close = @clo Where nindex = @in";
					statement = new SQLiteCommand(myquery,mycon);
					statement.Parameters.AddWithValue("@hi",high.ToString(CultureInfo.InvariantCulture));
					statement.Parameters.AddWithValue("@lo",low.ToString(CultureInfo.InvariantCulture));
					statement.Parameters.AddWithValue("@clo",close.ToString(CultureInfo.InvariantCulture));
					statement.Parameters.AddWithValue("@in",nindex);
					statement.ExecuteNonQuery();
					statement.Dispose();
					mycon.Close();
					
					//Candle was updated
					return true;
				}
			}else{
				//No candle exists		
			}
			statement_reader.Close();
			statement.Dispose();
			mycon.Close();
			return false;
		}
		
		public static void ClearMarketData(int market)
		{
			//Remove all market data for our market
			//This function is not performed by critical nodes
			//Market -1 means remove all markets data if there
			
			//Clear all the candles
			string myquery;
			SQLiteConnection mycon = new SQLiteConnection("Data Source=\""+App.App_Path+"/data/neblidex.db\";Version=3;");
			mycon.Open();
			
			SQLiteCommand statement = new SQLiteCommand("PRAGMA busy_timeout = 5000",mycon); //Create a transaction to make inserts faster
			statement.ExecuteNonQuery();
			statement.Dispose();
			
			//Delete the Candles Database as they have come out of sync and obtain new chart from another server
			myquery = "Delete From CANDLESTICKS7D";
			statement = new SQLiteCommand(myquery,mycon);
			statement.ExecuteNonQuery();
			statement.Dispose();
			
			myquery = "Delete From CANDLESTICKS24H";
			statement = new SQLiteCommand(myquery,mycon);
			statement.ExecuteNonQuery();
			statement.Dispose();
			mycon.Close();
			
			//Now clear the open orders for the market
			if(market > 0){
				lock(App.OpenOrderList[market]){
					App.OpenOrderList[market].Clear(); //We don't need to see those orders
				}
				lock(App.RecentTradeList[market]){
					App.RecentTradeList[market].Clear();
				}
			}else{
				for(int mark = 0;mark < App.total_markets;mark++){
					lock(App.OpenOrderList[mark]){
						App.OpenOrderList[mark].Clear(); //We don't need to see those orders
					}
					lock(App.RecentTradeList[mark]){
						App.RecentTradeList[mark].Clear();
					}					
				}
			}
			lock(App.ChartLastPrice){
				App.ChartLastPrice[0].Clear();
				App.ChartLastPrice[1].Clear();
			}

		}
		
		public static void PeriodicCandleMaker(object state)
		{
			//Update the candles at this set interval
			
			//Now move the candle watching time forward
			int utime = App.UTCTime();
			if(App.next_candle_time == 0){
				App.next_candle_time = utime + 60*15;
			}else{
				App.next_candle_time += 60*15;
			}
			
			//Set the 15 minute candle time to this
			App.ChartLastPrice15StartTime = App.next_candle_time - 60*15;
			
			//Because System timers are inprecise and lead to drift, we must update time manually
			int waittime = App.next_candle_time - utime;
			if(waittime < 0){waittime=0;} //Shouldn't be possible
			App.CandleTimer.Change(waittime*1000,Timeout.Infinite);

			lock(App.ChartLastPrice){
				string myquery="";
				double high,low,open,close;
				
				App.candle_15m_interval++;
				int end_time=1;
				if(App.candle_15m_interval == 6){
					App.candle_15m_interval = 0;
					end_time=2;
					//Create a candle using our lastpriceobject list for each market (if CN) or 1 market for regular node
				}
				
				int start_market = 0;
				int end_market = App.total_markets;
				if(App.critical_node == false){
					start_market = App.exchange_market; //Only store for
					end_market = start_market+1;
				}
	
				//CNs are only ones required to store all candle data from all markets
				//Do the same for the 15 minute lastpriceobject
				//Create a transaction
				SQLiteConnection mycon = new SQLiteConnection("Data Source=\""+App.App_Path+"/data/neblidex.db\";Version=3;");
				mycon.Open();
				
				//Set our busy timeout, so we wait if there are locks present
				SQLiteCommand statement = new SQLiteCommand("PRAGMA busy_timeout = 5000",mycon); //Create a transaction to make inserts faster
				statement.ExecuteNonQuery();
				statement.Dispose();
				
				statement = new SQLiteCommand("BEGIN TRANSACTION",mycon); //Create a transaction to make inserts faster
				statement.ExecuteNonQuery();
				statement.Dispose();
				
				for(int time = 0;time < end_time;time++){
					for(int market = start_market;market < end_market;market++){
						//Go through the 7 day table if necessary
						int numpts = App.ChartLastPrice[time].Count; //All the pounts for the timeline
						open = -1;
						close = -1;
						high = -1;
						low = -1;
						
						for(int pos=0;pos<App.ChartLastPrice[time].Count;pos++){ //This should be chronological
							if(App.ChartLastPrice[time][pos].market == market){
								double price = Convert.ToDouble(App.ChartLastPrice[time][pos].price);
								if(open < 0){open = price;}
								if(price > high){
									high = price;
								}
								if(low < 0 || price < low){
									low = price;
								}
								close = price; //The last price will be the close
								//Which will also be the open for the next candle
							}
						}
						
						//Then delete all the ones except the last one
						bool clear=false;
						//Reset all chart last prices
						//Remove all the prices except the last one, this will be our new open
						for(int pos = App.ChartLastPrice[time].Count-1; pos >= 0; pos--){
							if(App.ChartLastPrice[time][pos].market == market){
								if(clear == false){
									//This is the one to save, most recent lastpriceobject
									clear = true;
									close = Convert.ToDouble(App.ChartLastPrice[time][pos].price);
								}else if(clear == true){
									//Remove all else
									App.ChartLastPrice[time].RemoveAt(pos); //Take it out
								}
							}
						}

						if(market == App.exchange_market && App.main_window_loaded == true){
							//Now if this is the active market, add a new candle based on the charttimeline
							//Now modify the visible candles
							if(App.VisibleCandles.Count > 0 && close > 0){
								if(App.main_window.chart_timeline == 0 && time == 0){
									//24 Hr
									App.Candle can = new App.Candle(close);
									System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
										() =>
										{
											App.AddCandleShapes(can);
											App.main_window.PlaceCandleInChart(can);
										}));
								}else if(App.main_window.chart_timeline == 1 && time == 1){
									//Only place new candle on this timeline every 90 minutes
									App.Candle can = new App.Candle(close);
									System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(
										() =>
										{
											App.AddCandleShapes(can);
											App.main_window.PlaceCandleInChart(can);
										}));
								}
							}						
						}
						
						if(open > 0){
							//May not have any activity on that market yet
							//If there is at least 1 trade on market, this will add to database
							//Insert old candle into database
							int ctime = App.UTCTime();
							if(time == 0){
								ctime -= 60*15; //This candle started 15 minutes ago
								myquery = "Insert Into CANDLESTICKS24H";
							}else if(time == 1){
								ctime -= 60*90; //This candle started 90 minutes ago
								myquery = "Insert Into CANDLESTICKS7D";
							}
							
							//Insert to the candle database
							
							myquery += " (utctime, market, highprice, lowprice, open, close)";
							myquery += " Values (@time, @mark, @high, @low, @op, @clos);";
							statement = new SQLiteCommand(myquery,mycon);
							statement.Parameters.AddWithValue("@time",ctime);
							statement.Parameters.AddWithValue("@mark",market);
							statement.Parameters.AddWithValue("@high",high);
							statement.Parameters.AddWithValue("@low",low);
							statement.Parameters.AddWithValue("@op",open);
							statement.Parameters.AddWithValue("@clos",close);
							statement.ExecuteNonQuery();
							statement.Dispose();

						}
					}
				}
				
				
				//Close the connection
				statement = new SQLiteCommand("COMMIT TRANSACTION",mycon); //Close the transaction
				statement.ExecuteNonQuery();
				statement.Dispose();
				mycon.Close();
				
			}

			//Now calculate the fee as the chart has changed and send to all connected TNs
			//Calculate the CN Fee
			if(App.critical_node == true){
				App.CalculateCNFee();
				//Push this new fee to all connected pairs
				lock(App.DexConnectionList){
					for(int i = 0;i < App.DexConnectionList.Count;i++){
						if(App.DexConnectionList[i].outgoing == false && App.DexConnectionList[i].contype == 3 && App.DexConnectionList[i].version >= App.protocol_min_version){
							App.SendCNServerAction(App.DexConnectionList[i],53,"");
						}
					}
				}
				
				//If a critical node is pending right now, we must resync the chart again because candle data may be inaccurate
				if(App.critical_node_pending == true){
					App.NebliDexNetLog("Resyncing candles because potential candle information lost");
					App.reconnect_cn = true;
					App.cn_network_down = true; //This will force a resync
				}
				
				if(App.run_headless == true){
					//This is a heartbeat indicator
					int cn_online = App.CN_Nodes_By_IP.Count;
					string percent = String.Format(CultureInfo.InvariantCulture,"{0:0.###}",Math.Round(App.my_cn_weight*100,3));
					Console.WriteLine("Critical Node Status ("+App.UTCTime()+" UTC TIME): CNs Online: "+cn_online+", "+percent+"% Chance of Validating");
				}
			}
			
			//Finally check the electrum server sync
			App.CheckElectrumServerSync();
			App.CheckCNBlockHelperServerSync();
		}
		
		//Buttons and Options
		private void Change_Chart_Timeline(object sender, RoutedEventArgs e)
		{
			//Change Chart Timeline and reload
			if(Convert.ToString(Market_Percent.Content) == "LOADING..."){return;} 
			lock(App.ChartLastPrice){ //Do not touch the visible candles unless we know no one else is
				if((string)Chart_Time_Toggle.Content == "24 Hour"){
					//Change to 7 Day
					Chart_Time_Toggle.Content = "7 Day";
					chart_timeline = 1;
				}else{
					Chart_Time_Toggle.Content = "24 Hour";
					chart_timeline = 0;
				}
				UpdateCandles();
			}
		}
		
		private void Open_DNS_Seed(object sender, RoutedEventArgs e)
		{
		    SeedList dns_seed = new SeedList(App.DNS_SEED);
		    dns_seed.ShowDialog();
		}
		
		private void Open_Deposit(object sender, RoutedEventArgs e)
		{			
			Deposit dep = new Deposit();
		    dep.ShowDialog();
		}
		
		private void Open_Withdraw(object sender, RoutedEventArgs e)
		{
			Withdraw with = new Withdraw();
		    with.ShowDialog();
		}
		
		private void Open_Buy(object sender, RoutedEventArgs e)
		{
			if(App.critical_node == true){
				System.Windows.MessageBox.Show("Cannot Create An Order in Critical Node Mode");
				return;
			}
			if(Convert.ToString(Market_Percent.Content) == "LOADING..."){return;} //Cannot buy in between markets
			PlaceOrder buy = new PlaceOrder(0);
		    buy.ShowDialog();
		}
		
		private void Open_Sell(object sender, RoutedEventArgs e)
		{
			if(App.critical_node == true){
				System.Windows.MessageBox.Show("Cannot Create An Order in Critical Node Mode");
				return;
			}
			if(Convert.ToString(Market_Percent.Content) == "LOADING..."){return;} //Cannot buy in between markets
			PlaceOrder sell = new PlaceOrder(1);
		    sell.ShowDialog();
		}
		
		private async void Change_Market(object sender, SelectionChangedEventArgs e)
		{
			if(App.main_window_loaded == false){return;}
			if(Convert.ToString(Market_Percent.Content) == "LOADING..."){return;}  //Can't change market when waiting
			//First find which one was selected
			string market_string = (string)Market_Box.Items[Market_Box.SelectedIndex];
			int which_market = Selected_Market(market_string);
			
			if(which_market > -1){
				int oldmarket = App.exchange_market;
				App.exchange_market = which_market;
				
				Market_Box.IsEnabled = false;
				if(App.critical_node == false){
					Market_Percent.Content = "LOADING..."; //Put a loading status
					Market_Percent.Foreground = System.Windows.Media.Brushes.White;
					if(current_ui_look == 1){
						//Light
						Market_Percent.Foreground = System.Windows.Media.Brushes.Black;
					}else if(current_ui_look == 2){
						//Dark
						Market_Percent.Foreground = dark_ui_foreground;
					}
					//Clear the old candles and charts and reload the market data for this market
					await Task.Run(() => {ClearMarketData(oldmarket);App.GetCNMarketData(App.exchange_market);} );
				}
				Market_Box.IsEnabled = true; //Re-enable the box
				RefreshUI();
				Save_UI_Config(); //Save the UI Market Info
			}
		}
		
		private int Selected_Market(string mform)
		{
			for(int i=0;i < App.MarketList.Count;i++){
				if(mform == App.MarketList[i].trade_symbol+"/"+App.MarketList[i].base_symbol){
					return i;
				}
			}
			return -1;
		}
		
		private void Run_Background(object sender, RoutedEventArgs e)
		{
			//Hide the window and run the tray
            this.Hide();
            trayicon.Visible = true;
            trayicon.BalloonTipTitle = "Reminder:";
            trayicon.BalloonTipText = "NebliDex will continue to run in the background to keep your open orders from closing.";
            trayicon.BalloonTipIcon = ToolTipIcon.Info;
            trayicon.ShowBalloonTip(3000);
		}
		
		private void Close_Program(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
		
		private void Save_Backup_Dialog(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dia = new SaveFileDialog();
			dia.FileName = "account.dat";
			dia.Filter = "Dat files (*.dat)|*.dat|All files (*.*)|*.*";
			if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				//Make a copy of the wallet file here
				try {
					File.Copy(App.App_Path+"/data/account.dat",dia.FileName);
				} catch (Exception) {
					App.NebliDexNetLog("Failed to create wallet backup");
				}
			}
		}
		
		private async void Open_Backup_Dialog(object sender, RoutedEventArgs e)
		{
			
			if(App.MyOpenOrderList.Count > 0){
				System.Windows.MessageBox.Show("Cannot load wallet with open orders present.");
				return;
			}
			if(App.critical_node == true){
				System.Windows.MessageBox.Show("Cannot load wallet while as a critical node.");
				return;
			}
			
			MessageBoxResult result = System.Windows.MessageBox.Show("Importing a wallet will replace the current wallet. Do you want to continue?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
			{
				return;
			}
			
			if(App.CheckPendingPayment() == true){
				System.Windows.MessageBox.Show("There is at least one pending payment to this current address.");
				return;				
			}
			
			bool moveable = true;
			for(int i = 0;i < App.WalletList.Count;i++){
				if(App.WalletList[i].status != 0){
					moveable = false;break;
				}
			}
			if(moveable == false){
				System.Windows.MessageBox.Show("There is at least one wallet unavailable to change the current address","Notice!",MessageBoxButton.OK);
				return;			
			}
			
			OpenFileDialog dia = new OpenFileDialog();
			dia.FileName = "account.dat";
			dia.Filter = "Dat files (*.dat)|*.dat|All files (*.*)|*.*";
			if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				//Move all the files around to this new wallet and load it
				try {
					if(File.Exists(App.App_Path+"/data/account_old.dat") != false){
						File.Delete(App.App_Path+"/data/account_old.dat");
					}
					//Move the account.dat to the new location (old file) until the copy is complete
					File.Move(App.App_Path+"/data/account.dat",App.App_Path+"/data/account_old.dat");
					if(File.Exists(dia.FileName) == false){
						//Revert the changes
						File.Move(App.App_Path+"/data/account_old.dat",App.App_Path+"/data/account.dat");
						System.Windows.MessageBox.Show("Unable to import this wallet location.");
						return;
					}
					File.Copy(dia.FileName,App.App_Path+"/data/account.dat");
					App.my_wallet_pass = ""; //Remove the wallet password
				    App.CheckWallet(this); //Ran inline
				    await Task.Run(() => App.LoadWallet() );
				    Wallet_List.Items.Refresh();
				    //Now delete the old wallet
				    File.Delete(App.App_Path+"/data/account_old.dat");
				} catch (Exception) {
					App.NebliDexNetLog("Failed to load wallet");
					System.Windows.MessageBox.Show("Failed to load imported NebliDex wallet.");
				}
			}
		}
		
		private async void Request_Change_Address(object sender, RoutedEventArgs e)
		{
			//This will request a change to all the addresses
			if(App.MyOpenOrderList.Count > 0){
				System.Windows.MessageBox.Show("Cannot change addresses with open orders present.");
				return;
			}
			if(App.critical_node == true){
				System.Windows.MessageBox.Show("Cannot change addresses while as a critical node.");
				return;
			}
			
			MessageBoxResult result = System.Windows.MessageBox.Show("Are you sure you want to change all your wallet addresses?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result == MessageBoxResult.Yes)
			{
				await Task.Run(() => App.ChangeWalletAddresses()  );
			}
		}
		
		private void Request_ClearCNData(object sender, RoutedEventArgs e)
		{
			//This will request a CN table to be cleared
			MessageBoxResult result = System.Windows.MessageBox.Show("Are you sure you want to clear your CN fees history?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result == MessageBoxResult.Yes)
			{
				App.ClearAllCNFees();
			}
		}
		
		private void Toggle_Encryption(object sender, RoutedEventArgs e)
		{
			//This will bring up the prompt to encrypt or decrypt the wallet
			if(App.my_wallet_pass.Length > 0){
				//Encryption is present
			    UserPrompt p = new UserPrompt("Please enter your wallet password\nto decrypt wallet.",true); //Window
			    p.Owner = this;
			    p.ShowDialog();
			    if(p.final_response.Equals(App.my_wallet_pass) == false){
			    	System.Windows.MessageBox.Show("You've entered an incorrect password.");
			    }else{
			    	App.DecryptWalletKeys();
			    	App.my_wallet_pass = "";
			    	Wallet_Enc_Title.Header = "Encrypt Wallet";
			    }
			}else{
			    UserPrompt p = new UserPrompt("Please enter a new password\nto encrypt your wallet.",false); //Window
			    p.Owner = this;
			    p.ShowDialog();
			    App.my_wallet_pass = p.final_response;
			    if(App.my_wallet_pass.Length > 0){
			    	App.EncryptWalletKeys();
			    	Wallet_Enc_Title.Header = "Decrypt Wallet";
			    }
			}
		}
		
		private async void Request_Cancel_Order(object sender, RoutedEventArgs e)
		{
			//This will cancel an order, however it will not stop a pending payment after my transaction has broadcasted
			//Also for market orders, will attempt to cancel the order requests
			var button = sender as System.Windows.Controls.Button;
			int index = Open_Orders_List.Items.IndexOf(button.DataContext);
			if(index < 0){return;}
			App.OpenOrder ord = (App.OpenOrder)Open_Orders_List.Items[index];
			if(ord.order_stage >= 3){
				//The maker has an order in which it is waiting for the taker to redeem balance
				System.Windows.MessageBox.Show("Your order is currently involved in a trade. Please try again later.");
				return;
			}
			
			button.IsEnabled = false;
			await Task.Run(() => App.CancelMyOrder(ord)  );
			button.IsEnabled = true;
			
			Open_Orders_List.Items.Refresh();
		}
		
		private void Request_CN_Status(object sender, RoutedEventArgs e)
		{
			
			if(App.MyOpenOrderList.Count > 0){
				System.Windows.MessageBox.Show("Cannot go into Critical Node Mode with open orders.");
				return;
			}
			
			//Request CN status from another CN, if none available, alert neblidex.xyz, you are only CN
			//NebliDex will check this claim against blockchain
			Intro win = new Intro();
			win.Owner = this;
			win.Intro_Status.Content = "";
			bool old_critical_node = App.critical_node;
			Task.Run(() => App.ToggleCNStatus(win) );
			win.ShowDialog();
			if(App.critical_node == true){
				this.Title = "NebliDex: A Decentralized Neblio Exchange "+App.version_text+" (Critical Node Running)";
				CN_Tab.Visibility = Visibility.Visible;
				CN_Menu_Clear.Visibility = Visibility.Visible;
				Export_CN_Fee.Visibility = Visibility.Visible;
				this.Menu_Item_CN.IsChecked = true;
			}else{
				this.Title = "NebliDex: A Decentralized Neblio Exchange "+App.version_text;
				CN_Tab.Visibility = Visibility.Collapsed;
				CN_Menu_Clear.Visibility = Visibility.Collapsed;
				Export_CN_Fee.Visibility = Visibility.Collapsed;
				this.Menu_Item_CN.IsChecked = false;
			}
			if(old_critical_node != App.critical_node){
				RefreshUI();
			}
		}
		
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			App.main_window_loaded = true;
			if(current_ui_look != 0){
				//Now change the theme
				Change_UI_Look(current_ui_look);
			}			
		}
		
		private void Window_Closing(object sender, CancelEventArgs arg)
		{
			string msg = "";
			if(App.MyOpenOrderList.Count > 0){
				msg = "You still have an open order. Are you sure you want to exit NebliDex?";
			}else if(App.cn_num_validating_tx > 0){
				msg = "You are still validating some transactions. Are you sure you want to exit NebliDex?";
			}
			
			//Check to see if there are any pending orders that are being matched
			//Since Atomic Swaps are timelocked, it is not advised to leave program when actively involved in swap
			lock(App.MyOpenOrderList){
		    	for(int i = 0;i < App.MyOpenOrderList.Count;i++){
					if(App.MyOpenOrderList[i].order_stage >= 3 && App.MyOpenOrderList[i].is_request == false){
						msg = "You are involved in a trade. If you close now, you may lose trade amount. Are you sure you want to exit NebliDex?";
		    			break;
		    		}
		    	}
		    }
			
			if(App.CheckPendingTrade() == true){
				msg = "You are involved in a trade. If you close now, you may lose trade amount. Are you sure you want to exit NebliDex?";
			}
			
			//This will be called when the window is closing
			if(msg.Length > 0){
				MessageBoxResult result = System.Windows.MessageBox.Show(msg, "Confirmation",System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (result == MessageBoxResult.No)
				{
					arg.Cancel = true;
				}
			}
		}
		
		public void LegalWarning()
		{
			System.Windows.MessageBox.Show("Do not use NebliDex if its use is unlawful in your local jurisdiction.\nCheck your local laws before use.","DISCLAIMER");
			return;
		}
		
		private async void Save_All_Trades(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dia = new SaveFileDialog();
			dia.FileName = "tradehistory.csv";
			dia.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
			if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				//CVS Format
				//Time, Market, Price, Bought, Sold
				await Task.Run(() => App.ExportTradeHistory(dia.FileName)  );
			}
		}
		
		private async void Save_All_CNHx(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dia = new SaveFileDialog();
			dia.FileName = "feehistory.csv";
			dia.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
			if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				//CVS Format
				//Time, Market, Fee
				await Task.Run(() => App.ExportCNFeeHistory(dia.FileName)  );
			}
		}
		
		public async void Prompt_Load_Saved_Orders()
		{
			MessageBoxResult result = System.Windows.MessageBox.Show("Would you like to repost your previously loaded open orders?", "Load Saved Orders",System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result == MessageBoxResult.Yes)
			{
				await Task.Run(() => App.LoadSavedOrders()  );
			}else{
				await Task.Run(() => App.ClearSavedOrders()  );
			}
		}
		
		public void Select_UI_Look(object sender, RoutedEventArgs e)
		{
			System.Windows.Controls.MenuItem mi = e.Source as System.Windows.Controls.MenuItem;
			if(mi.Name == "Default_UILook_Option"){
				Change_UI_Look(0);
			}else if(mi.Name == "Light_UILook_Option"){
				Change_UI_Look(1);
			}else if(mi.Name == "Dark_UILook_Option"){
				Change_UI_Look(2);
			}
		}
		
		public void Change_UI_Look(int look)
		{
			if(look == 0){
				Default_UILook_Option.IsChecked = true;
				Light_UILook_Option.IsChecked = false;
				Dark_UILook_Option.IsChecked = false;
				
				Main_Grid_View.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(191,191,191));
				Market_Box_View.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(10,24,44));
				Market_Box.Style = default_marketbox_style;
				if(Market_Percent.Foreground == System.Windows.Media.Brushes.Black || Market_Percent.Foreground == dark_ui_foreground){
					Market_Percent.Foreground = System.Windows.Media.Brushes.White;
				}
				if(Market_Percent.Foreground == darkgreen_candle){
					Market_Percent.Foreground = green_candle;
				}
				Selling_View.ClearValue(System.Windows.Controls.ListView.BackgroundProperty);
				Selling_View.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Buying_View.ClearValue(System.Windows.Controls.ListView.BackgroundProperty);
				Buying_View.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				
				Wallet_Panel_View.BorderBrush = System.Windows.Media.Brushes.Gray;
				Wallet_Panel_View.Background = default_ui_gradient;
				
				Buy_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				Sell_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				Deposit_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				Withdraw_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				
				Wallet_List.Background = default_ui_gradient;
				Wallet_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);

				//User data information
				User_Data_View.ClearValue(System.Windows.Controls.TabControl.BackgroundProperty);
				User_Data_View.ClearValue(System.Windows.Controls.TabControl.BorderBrushProperty);
				Recent_Trade_List.Background = default_ui_gradient;
				Recent_Trade_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Trade_History_List.Background = default_ui_gradient;
				Trade_History_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Open_Orders_List.Background = default_ui_gradient;
				Open_Orders_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				CN_Tx_List.Background = default_ui_gradient;
				CN_Tx_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				
				Chart_Canvas_Border_View.Background = default_canvas_border;
				Chart_Canvas.Background = default_canvas_color;
				Chart_Last_Price.Foreground = System.Windows.Media.Brushes.White;
				Chart_Mouse_Price.Foreground = System.Windows.Media.Brushes.White;
				Chart_Time_Toggle.Background = default_canvas_color;
				Chart_Time_Toggle.Foreground = System.Windows.Media.Brushes.White;		

				//Update the candles color
				for(int i=0;i < App.VisibleCandles.Count;i++){
					App.Candle can = App.VisibleCandles[i];
					if(can.open > can.close){
						//Red candle
						can.rect.Fill = red_candle;
					}else{
						can.rect.Fill = green_candle;
					}
					can.line.Stroke =  System.Windows.Media.Brushes.White;
				}
			}else if(look == 1){ //Light
				Light_UILook_Option.IsChecked = true;
				Default_UILook_Option.IsChecked = false;
				Dark_UILook_Option.IsChecked = false;

				Main_Grid_View.Background = System.Windows.Media.Brushes.White;
				Market_Box_View.Background = null;
				Market_Box.Style = FindResource("Light_ComboBoxFlatStyle") as Style;
				if(Market_Percent.Foreground == System.Windows.Media.Brushes.White || Market_Percent.Foreground == dark_ui_foreground){
					Market_Percent.Foreground = System.Windows.Media.Brushes.Black;
				}
				if(Market_Percent.Foreground == green_candle){
					Market_Percent.Foreground = darkgreen_candle;
				}
				Selling_View.Background = System.Windows.Media.Brushes.White;
				Buying_View.Background = System.Windows.Media.Brushes.White;
				Selling_View.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Buying_View.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				
				Wallet_Panel_View.Background = System.Windows.Media.Brushes.White;
				Wallet_Panel_View.BorderBrush = System.Windows.Media.Brushes.Gray;
				
				Buy_Button.Background = System.Windows.Media.Brushes.White;
				Sell_Button.Background = System.Windows.Media.Brushes.White;
				Deposit_Button.Background = System.Windows.Media.Brushes.White;
				Withdraw_Button.Background = System.Windows.Media.Brushes.White;				
				
				Wallet_List.Background = System.Windows.Media.Brushes.White;
				Wallet_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				
				User_Data_View.Background = System.Windows.Media.Brushes.White;
				User_Data_View.ClearValue(System.Windows.Controls.TabControl.BorderBrushProperty);
				Recent_Trade_List.Background = System.Windows.Media.Brushes.White;
				Recent_Trade_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Trade_History_List.Background = System.Windows.Media.Brushes.White;
				Trade_History_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				Open_Orders_List.Background = System.Windows.Media.Brushes.White;
				Open_Orders_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				CN_Tx_List.Background = System.Windows.Media.Brushes.White;
				CN_Tx_List.ClearValue(System.Windows.Controls.ListView.BorderBrushProperty);
				
				Chart_Canvas_Border_View.Background = System.Windows.Media.Brushes.White;
				Chart_Canvas.Background = System.Windows.Media.Brushes.White;
				Chart_Last_Price.Foreground = System.Windows.Media.Brushes.Black;
				Chart_Mouse_Price.Foreground = System.Windows.Media.Brushes.Black;
				Chart_Time_Toggle.Background = System.Windows.Media.Brushes.White;
				Chart_Time_Toggle.Foreground = System.Windows.Media.Brushes.Black;

				//Update the candles color
				for(int i=0;i < App.VisibleCandles.Count;i++){
					App.Candle can = App.VisibleCandles[i];
					if(can.open > can.close){
						//Red candle
						can.rect.Fill = red_candle;
					}else{
						can.rect.Fill = darkgreen_candle;
					}
					can.line.Stroke =  System.Windows.Media.Brushes.Gray;
				}
			}else if(look == 2){ //Dark
				Dark_UILook_Option.IsChecked = true;
				Light_UILook_Option.IsChecked = false;
				Default_UILook_Option.IsChecked = false;
				
				Main_Grid_View.Background = System.Windows.Media.Brushes.Black;
				Market_Box_View.Background = null;
				Market_Box.Style = FindResource("Dark_ComboBoxFlatStyle") as Style;
				if(Market_Percent.Foreground == System.Windows.Media.Brushes.Black || Market_Percent.Foreground == System.Windows.Media.Brushes.White){
					//Only change if already white
					Market_Percent.Foreground = dark_ui_foreground;
				}
				if(Market_Percent.Foreground == darkgreen_candle){
					Market_Percent.Foreground = green_candle;
				}
				Selling_View.Background = dark_ui_panel;
				Selling_View.BorderBrush = dark_ui_panel;
				Buying_View.Background = dark_ui_panel;
				Buying_View.BorderBrush = dark_ui_panel;
				
				Wallet_Panel_View.Background = dark_ui_panel;
				Wallet_Panel_View.BorderBrush = dark_ui_panel;
				
				Buy_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				Sell_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);				
				Deposit_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				Withdraw_Button.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
				
				Wallet_List.Background = dark_ui_panel;
				Wallet_List.BorderBrush = dark_ui_panel;
				
				User_Data_View.Background = dark_ui_panel;
				User_Data_View.BorderBrush = dark_ui_panel;
				Recent_Trade_List.Background = dark_ui_panel;
				Recent_Trade_List.BorderBrush = dark_ui_panel;
				Trade_History_List.Background = dark_ui_panel;
				Trade_History_List.BorderBrush = dark_ui_panel;
				Open_Orders_List.Background = dark_ui_panel;
				Open_Orders_List.BorderBrush = dark_ui_panel;
				CN_Tx_List.Background = dark_ui_panel;
				CN_Tx_List.BorderBrush = dark_ui_panel;

				Chart_Canvas_Border_View.Background = dark_ui_panel;
				Chart_Canvas.Background = dark_ui_panel;
				Chart_Last_Price.Foreground = dark_ui_foreground;
				Chart_Mouse_Price.Foreground = dark_ui_foreground;
				Chart_Time_Toggle.ClearValue(System.Windows.Controls.Button.BackgroundProperty);		
				Chart_Time_Toggle.ClearValue(System.Windows.Controls.Button.ForegroundProperty);

				//Update the candles colors
				for(int i=0;i < App.VisibleCandles.Count;i++){
					App.Candle can = App.VisibleCandles[i];
					if(can.open > can.close){
						//Red candle
						can.rect.Fill = red_candle;
					}else{
						can.rect.Fill = green_candle;
					}
					can.line.Stroke =  System.Windows.Media.Brushes.White;
				}
			}
			
			current_ui_look = look;

			Save_UI_Config();
		}
		
		private void Save_UI_Config()
		{
			//Saves the UI information to a file
			try {
		        using (System.IO.StreamWriter file = 
		            new System.IO.StreamWriter(@App.App_Path+"/data/ui.ini", false))
		        {
					file.WriteLine("Version = 1");
					string look = "";
					if(current_ui_look == 0){
						look = "Default";
					}else if(current_ui_look == 1){
						look = "Light";
					}else if(current_ui_look == 2){
						look = "Dark";
					}
					file.WriteLine("Look = "+look);
					file.WriteLine("Default_Market = "+App.exchange_market);
		        }	
			} catch (Exception) {
				App.NebliDexNetLog("Failed to save user interface data to file");
			}
		}

		public static void Load_UI_Config()
		{
			if(File.Exists(App.App_Path+"/data/ui.ini") == false){
				return; //Use the default themes as no UI file exists
			}
			int version = 0;
			try {
		        using (System.IO.StreamReader file = 
		            new System.IO.StreamReader(@App.App_Path+"/data/ui.ini", false))
		        {
					while(!file.EndOfStream){
						string line_data = file.ReadLine();
						line_data = line_data.ToLower();
						if(line_data.IndexOf("=") > -1){
							string[] variables = line_data.Split('=');
							string key = variables[0].Trim();
							string data = variables[1].Trim();
							if(key == "version"){
								version = Convert.ToInt32(data);
							}else if(key == "look"){
								if(data == "default"){
									App.default_ui_look = 0;
								}else if(data == "light"){
									App.default_ui_look = 1;
								}else if(data == "dark"){
									App.default_ui_look = 2;
								}
							}else if(key == "default_market"){
								App.exchange_market = Convert.ToInt32(data);
								if(App.exchange_market > App.total_markets){App.exchange_market = App.total_markets;}
							}
						}
					}
				}	
			} catch (Exception) {
				App.NebliDexNetLog("Failed to read user interface data from file");
			}			
		}
		
	}
}