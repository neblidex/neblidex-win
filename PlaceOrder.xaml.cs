/*
 * Created by SharpDevelop.
 * User: David
 * Date: 2/14/2018
 * Time: 3:51 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Threading.Tasks;

namespace NebliDex
{
	/// <summary>
	/// Interaction logic for PlaceOrder.xaml
	/// </summary>
	public partial class PlaceOrder : Window
	{
		int order_type=0;
		
		public PlaceOrder(int type)
		{
			InitializeComponent();
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			order_type = type;
			
			decimal balance = App.GetMarketBalance(App.exchange_market,type);
			
			decimal price = 0;
			if(App.ChartLastPrice[0].Count > 0){
				
				//Get the last trade price for the market as default (on 24 hr chart)
				for(int i=App.ChartLastPrice[0].Count-1;i >= 0;i--){
					if(App.ChartLastPrice[0][i].market == App.exchange_market){
						price = App.ChartLastPrice[0][i].price;break;
					}
				}
				Price_Input.Text = ""+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",price);
			}
			
			string trade_symbol = App.MarketList[App.exchange_market].trade_symbol;
			string base_symbol = App.MarketList[App.exchange_market].base_symbol;
			
			if(type == 0){
				//Buy Order
				Order_Header.Content = "Buy "+trade_symbol;
				My_Balance.Content = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",balance)+" "+base_symbol;
				Price_Header.Content = "Price ("+base_symbol+"):";
				Amount_Header.Content = "Amount ("+trade_symbol+"):";
				Min_Amount_Header.Content = "Minimum Match ("+trade_symbol+"):";
				Total_Header.Content = "Total Cost ("+base_symbol+"):";
			}else{
				//Sell Order
				Order_Header.Content = "Sell "+trade_symbol;
				My_Balance.Content = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",balance)+" "+trade_symbol;
				Price_Header.Content = "Price ("+base_symbol+"):";
				Amount_Header.Content = "Amount ("+trade_symbol+"):";
				Min_Amount_Header.Content = "Minimum Match ("+trade_symbol+"):";
				Total_Header.Content = "Total Receive ("+base_symbol+"):";
			}
			
		}
		
        private void Price_KeyUp(object sender, KeyEventArgs e)
        {
        	if(App.IsNumber(Price_Input.Text) == false){return;}
        	decimal price = decimal.Parse(Price_Input.Text,CultureInfo.InvariantCulture);
        	if(price <= 0){return;}
        	
        	if(App.IsNumber(Amount_Input.Text) == false){return;}
        	decimal amount = decimal.Parse(Amount_Input.Text,CultureInfo.InvariantCulture);
        	if(amount <= 0){return;}
        	Total_Input.Text = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",amount*price);
        }
		
        private void Amount_KeyUp(object sender, KeyEventArgs e)
        {
        	if(App.IsNumber(Price_Input.Text) == false){return;}
        	decimal price = decimal.Parse(Price_Input.Text,CultureInfo.InvariantCulture);
        	if(price <= 0){return;}
        	
        	if(App.IsNumber(Amount_Input.Text) == false){return;}
        	decimal amount = decimal.Parse(Amount_Input.Text,CultureInfo.InvariantCulture);
        	if(amount <= 0){return;}
        	Total_Input.Text = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",amount*price);
        	//The default minimum
        	decimal min_amount = amount / 100m;
        	if(App.MarketList[App.exchange_market].trade_wallet > 2){
        		min_amount = Math.Round(min_amount); //Round to nearest whole number
        		if(min_amount == 0){min_amount = 1;}
        	}
        	Min_Amount_Input.Text = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",min_amount);
        }
        
        private void Total_KeyUp(object sender, KeyEventArgs e)
        {
        	if(App.IsNumber(Price_Input.Text) == false){return;}
        	decimal price = decimal.Parse(Price_Input.Text,CultureInfo.InvariantCulture);
        	if(price <= 0){return;}
        	
        	if(App.IsNumber(Total_Input.Text) == false){return;}
        	decimal total = decimal.Parse(Total_Input.Text,CultureInfo.InvariantCulture);
        	if(total <= 0){return;}
        	decimal amount = total/price;
          	if(App.MarketList[App.exchange_market].trade_wallet > 2){
        		amount = Math.Round(amount); //Round to nearest whole number
        		if(amount == 0){amount = 1;}
        	}
        	Amount_Input.Text = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",amount);
        	decimal min_amount = amount / 100;
        	if(App.MarketList[App.exchange_market].trade_wallet > 2){
        		min_amount = Math.Round(min_amount); //Round to nearest whole number
        		if(min_amount == 0){min_amount = 1;}
        	}
        	Min_Amount_Input.Text = String.Format(CultureInfo.InvariantCulture,"{0:0.########}",min_amount);
        }
        
        private async void Make_Order(object sender, RoutedEventArgs e)
		{
        	//Create our order!
        	//Get the price
        	if(App.IsNumber(Price_Input.Text) == false){return;}
        	if(Price_Input.Text.IndexOf(",") >= 0){
        		MessageBox.Show("NebliDex does not recognize commas for decimals at this time.","Notice!",MessageBoxButton.OK);
        		return;
        	}
        	decimal price = decimal.Parse(Price_Input.Text,CultureInfo.InvariantCulture);
        	if(price <= 0){return;}
        	if(price > App.max_order_price){
        		//Price cannot exceed the max
        		MessageBox.Show("This price is higher than the maximum price of 10 000 000","Notice!",MessageBoxButton.OK);
        		return;        		
        	}
        	
        	//Get the amount
        	if(App.IsNumber(Amount_Input.Text) == false){return;}
        	if(Amount_Input.Text.IndexOf(",") >= 0){
        		MessageBox.Show("NebliDex does not recognize commas for decimals at this time.","Notice!",MessageBoxButton.OK);
        		return;
        	}
        	decimal amount = decimal.Parse(Amount_Input.Text,CultureInfo.InvariantCulture);
        	if(amount <= 0){return;}
        	
         	if(App.IsNumber(Min_Amount_Input.Text) == false){return;}
        	if(Min_Amount_Input.Text.IndexOf(",") >= 0){
        		MessageBox.Show("NebliDex does not recognize commas for decimals at this time.","Notice!",MessageBoxButton.OK);
        		return;
        	}
        	decimal min_amount = decimal.Parse(Min_Amount_Input.Text,CultureInfo.InvariantCulture);
        	if(min_amount <= 0){
        		MessageBox.Show("The minimum amount is too small.","Notice!",MessageBoxButton.OK);
        		return;
        	}
        	if(min_amount > amount){
        		MessageBox.Show("The minimum amount cannot be greater than the amount.","Notice!",MessageBoxButton.OK);
        		return;        		
        	}
        	
        	decimal total = Math.Round(price*amount,8);
        	if(Total_Input.Text.IndexOf(",") >= 0){
        		MessageBox.Show("NebliDex does not recognize commas for decimals at this time.","Notice!",MessageBoxButton.OK);
        		return;
        	}
        	
        	if(App.MarketList[App.exchange_market].base_wallet == 3 || App.MarketList[App.exchange_market].trade_wallet == 3){
	        	//Make sure amount is greater than ndexfee x 2
	        	if(amount < App.ndex_fee*2){
	        		MessageBox.Show("This order amount is too small. Must be at least twice the CN fee ("+String.Format(CultureInfo.InvariantCulture,"{0:0.########}",App.ndex_fee*2)+" NDEX)","Notice!",MessageBoxButton.OK);
	        		return;
	        	}
        	}
        	
        	int wallet=0;
        	string msg="";
        	bool good = false;
        	if(order_type == 0){
        		//This is a buy order we are making, so we need base market balance
        		wallet = App.MarketList[App.exchange_market].base_wallet;
        		good = App.CheckWalletBalance(wallet,total,ref msg);
        		if(good == true){
        			//Now check the fees
        			good = App.CheckMarketFees(App.exchange_market,order_type,total,ref msg,false);
        		}
        	}else{
        		//Selling the trade wallet amount
        		wallet = App.MarketList[App.exchange_market].trade_wallet;
        		good = App.CheckWalletBalance(wallet,amount,ref msg);
        		if(good == true){
        			good = App.CheckMarketFees(App.exchange_market,order_type,amount,ref msg,false);
        		}
        	}
        	
			//Show error messsage if balance not available
			if(good == false){
				//Not enough funds or wallet unavailable
				MessageBox.Show(msg,"Notice!",MessageBoxButton.OK);
				return;
			}
			
			//Make sure that total is greater than block rates for both markets
			decimal block_fee1 = 0;
			decimal block_fee2 = 0;
			if(App.MarketList[App.exchange_market].trade_wallet > 2 || App.MarketList[App.exchange_market].trade_wallet == 0){
				block_fee1 = App.blockchain_fee[0]; //Neblio fee
			}
			if(App.MarketList[App.exchange_market].base_wallet >= 0 && App.MarketList[App.exchange_market].base_wallet < 3){
				block_fee2 = App.blockchain_fee[App.MarketList[App.exchange_market].base_wallet]; //Base fee
			}
			if(total < block_fee1 || total < block_fee2 || amount < block_fee1 || amount < block_fee2){
				//The trade amount is too small
				MessageBox.Show("This trade amount is too small to create because it is lower than the blockchain fee.","Notice!",MessageBoxButton.OK);
				return;  				
			}
			
        	//Because tokens are indivisible at the moment, amounts can only be in whole numbers
        	if(App.MarketList[App.exchange_market].trade_wallet > 2){
        		if(Math.Abs(Math.Round(amount)-amount) > 0){
					MessageBox.Show("All NTP1 tokens are indivisible at this time. Must be whole amounts.","Notice!",MessageBoxButton.OK);
					return;        			
        		}
        		amount = Math.Round(amount);
        		
        		if(Math.Abs(Math.Round(min_amount)-min_amount) > 0){
					MessageBox.Show("All NTP1 tokens are indivisible at this time. Must be whole minimum amounts.","Notice!",MessageBoxButton.OK);
					return;        			
        		}
        		min_amount = Math.Round(min_amount);
        	}
			
			//Check to see if any other open orders of mine
			if(App.MyOpenOrderList.Count >= App.total_markets){
				MessageBox.Show("You have exceed the maximum amount ("+App.total_markets+") of open orders.","Notice!",MessageBoxButton.OK);
				return;
			}
			
			App.OpenOrder ord = new App.OpenOrder();
			ord.order_nonce = App.GenerateHexNonce(32);
			ord.market = App.exchange_market;
			ord.type = order_type;
			ord.price = Math.Round(price,8);
			ord.amount = Math.Round(amount,8);
			ord.minimum_amount = Math.Round(min_amount,8);
			ord.original_amount = amount;
			ord.order_stage = 0;
			ord.my_order = true; //Very important, it defines how much the program can sign automatically
			
			//Try to submit order to CN
			Order_Button.IsEnabled = false;
			Order_Button.Content = "Contacting CN..."; //This will allow us to wait longer
			bool worked = await Task.Run(() => App.SubmitMyOrder(ord,null) );
			if(worked == true){
				//Add to lists and close order
				lock(App.MyOpenOrderList){
					App.MyOpenOrderList.Add(ord); //Add to our own personal list
				}
				lock(App.OpenOrderList[App.exchange_market]){
					App.OpenOrderList[App.exchange_market].Add(ord);
				}
				App.main_window.AddOrderToView(ord);
				App.main_window.Open_Orders_List.Items.Refresh();
				App.AddSavedOrder(ord);
				this.Close();return;
			}
			Order_Button.Content = "Create Order";
			Order_Button.IsEnabled = true;
        }
	}
}